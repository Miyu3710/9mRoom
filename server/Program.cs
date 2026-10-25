using System.Net.WebSockets;
using System.Reflection;
using System.Security.Cryptography;
using NineMRoom.Server;

const string AdminCookie = "nm_admin";
var serverVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(options =>
{
    options.SingleLine = true;
    options.TimestampFormat = "HH:mm:ss ";
});

var app = builder.Build();
var databasePath = Environment.GetEnvironmentVariable("NINEMROOM_DB");
if (string.IsNullOrWhiteSpace(databasePath))
{
    databasePath = Path.Combine(app.Environment.ContentRootPath, "data", "9mroom.db");
}

var database = new ServerDatabase(databasePath);
var reconnectGraceMs = 120_000L;
if (long.TryParse(
        Environment.GetEnvironmentVariable("NINEMROOM_RECONNECT_GRACE_MS"),
        out var configuredGrace) &&
    configuredGrace >= 1_000)
{
    reconnectGraceMs = configuredGrace;
}

var registry = new RoomRegistry(
    reconnectGraceMs,
    message => app.Logger.LogInformation("{Event}", message));
app.Logger.LogInformation("database={Path} reconnectGraceMs={Grace}", databasePath, reconnectGraceMs);

if (!database.HasAdmins())
{
    var username = Environment.GetEnvironmentVariable("NINEMROOM_ADMIN_USER");
    if (string.IsNullOrWhiteSpace(username))
    {
        username = "admin";
    }

    var password = Environment.GetEnvironmentVariable("NINEMROOM_ADMIN_PASSWORD");
    var generated = string.IsNullOrWhiteSpace(password);
    if (generated)
    {
        password = Convert.ToHexString(RandomNumberGenerator.GetBytes(9)).ToLowerInvariant();
    }

    database.CreateAdmin(username, password!, isSuper: true);
    if (generated)
    {
        app.Logger.LogWarning(
            "首次启动，已创建管理员 {User}，密码 {Password}；请登录 /admin 后妥善保存",
            username,
            password);
    }
    else
    {
        app.Logger.LogInformation("首次启动，已创建管理员 {User}", username);
    }
}

AdminRecord? CurrentAdmin(HttpContext context) =>
    database.SessionAdmin(context.Request.Cookies[AdminCookie]);

app.UseWebSockets(new WebSocketOptions { KeepAliveInterval = TimeSpan.FromSeconds(20) });
app.UseStaticFiles();

// 调试台不再占用站点根路径，统一走 /debug；静态资源也在 /debug/ 下。
var debugIndex = Path.Combine(
    app.Environment.WebRootPath ?? Path.Combine(app.Environment.ContentRootPath, "wwwroot"),
    "debug",
    "index.html");
app.MapGet("/debug", () => Results.File(debugIndex, "text/html; charset=utf-8"));

app.MapGet("/healthz", () => Results.Ok(new
{
    ok = true,
    version = serverVersion,
    serverTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
    rooms = database.ListRooms().Count,
    members = registry.MemberCount
}));

var debugEnabled = app.Environment.IsDevelopment() ||
    string.Equals(
        Environment.GetEnvironmentVariable("NINEMROOM_DEBUG"),
        "1",
        StringComparison.OrdinalIgnoreCase);
if (debugEnabled)
{
    app.MapGet("/debug/room/{id}", (string id) =>
    {
        var room = registry.DebugRoom(id);
        return room is null ? Results.NotFound() : Results.Ok(room);
    });
}

app.MapGet("/api/rooms/{code}", (string code) =>
{
    var normalized = code.Trim().ToLowerInvariant();
    if (normalized.Length is 0 or > 64)
    {
        return Results.BadRequest(new { error = "invalid room code" });
    }

    var room = database.GetRoom(normalized);
    return Results.Ok(new
    {
        code = normalized,
        exists = room is not null,
        kind = room?.Kind ?? string.Empty,
        name = room?.Name ?? string.Empty,
        online = registry.OnlineCount(normalized)
    });
});

app.MapGet("/admin", (HttpContext context) =>
{
    var admin = CurrentAdmin(context);
    if (admin is null)
    {
        return Results.Content(
            AdminPage.Login(context.Request.Query["error"]),
            "text/html; charset=utf-8");
    }

    return Results.Content(
        AdminPage.Rooms(
            admin,
            database.ListRooms(),
            registry.OnlineCount,
            database.ListAdmins(),
            context.Request.Query["error"]),
        "text/html; charset=utf-8");
});

app.MapPost("/admin/login", async (HttpContext context) =>
{
    var form = await context.Request.ReadFormAsync();
    var admin = database.Authenticate(
        form["username"].ToString(),
        form["password"].ToString());

    if (admin is null)
    {
        return Results.Redirect("/admin?error=" + Uri.EscapeDataString("用户名或密码错误"));
    }

    var token = database.CreateSession(admin.Id);
    var forceSecure = string.Equals(
        Environment.GetEnvironmentVariable("NINEMROOM_COOKIE_SECURE"),
        "1",
        StringComparison.OrdinalIgnoreCase);
    context.Response.Cookies.Append(AdminCookie, token, new CookieOptions
    {
        HttpOnly = true,
        Secure = forceSecure || context.Request.IsHttps,
        SameSite = SameSiteMode.Lax,
        Path = "/",
        Expires = DateTimeOffset.UtcNow.AddHours(12)
    });
    return Results.Redirect("/admin");
});

app.MapPost("/admin/logout", (HttpContext context) =>
{
    database.DeleteSession(context.Request.Cookies[AdminCookie]);
    context.Response.Cookies.Delete(AdminCookie);
    return Results.Redirect("/admin");
});

app.MapPost("/admin/rooms/create", async (HttpContext context) =>
{
    if (CurrentAdmin(context) is null)
    {
        return Results.Redirect("/admin");
    }

    var form = await context.Request.ReadFormAsync();
    var code = form["code"].ToString().Trim().ToLowerInvariant();
    var name = form["name"].ToString().Trim();
    if (code.Length is 0 or > 64 ||
        !code.All(character => char.IsAsciiLetterOrDigit(character) || character == '-') ||
        name.Length is 0 or > 40)
    {
        return Results.Redirect("/admin?error=" + Uri.EscapeDataString("房间码或名称不合法"));
    }

    if (!database.CreateRoom(code, name, "server", CurrentAdmin(context)?.Id))
    {
        return Results.Redirect("/admin?error=" + Uri.EscapeDataString("房间码已存在"));
    }

    return Results.Redirect("/admin");
});

app.MapPost("/admin/rooms/delete", async (HttpContext context) =>
{
    if (CurrentAdmin(context) is null)
    {
        return Results.Redirect("/admin");
    }

    var form = await context.Request.ReadFormAsync();
    var code = form["code"].ToString().Trim().ToLowerInvariant();
    if (database.DeleteRoom(code))
    {
        await registry.DropRoomAsync(code);
    }

    return Results.Redirect("/admin");
});

app.MapPost("/admin/admins/create", async (HttpContext context) =>
{
    var current = CurrentAdmin(context);
    if (current is null)
    {
        return Results.Redirect("/admin");
    }

    if (!current.IsSuper)
    {
        return Results.Redirect("/admin?error=" + Uri.EscapeDataString("只有超级管理员可以创建管理员"));
    }

    var form = await context.Request.ReadFormAsync();
    var username = form["username"].ToString().Trim();
    var password = form["password"].ToString();
    if (username.Length is 0 or > 32 ||
        username.Any(char.IsWhiteSpace) ||
        password.Length is < 8 or > 128)
    {
        return Results.Redirect("/admin?error=" + Uri.EscapeDataString("用户名需为 1–32 位且不含空格，密码至少 8 位"));
    }

    if (database.CreateAdmin(username, password, isSuper: false) is null)
    {
        return Results.Redirect("/admin?error=" + Uri.EscapeDataString("用户名已存在"));
    }

    return Results.Redirect("/admin");
});

app.MapPost("/admin/admins/password", async (HttpContext context) =>
{
    var current = CurrentAdmin(context);
    if (current is null)
    {
        return Results.Redirect("/admin");
    }

    if (!current.IsSuper)
    {
        return Results.Redirect("/admin?error=" + Uri.EscapeDataString("只有超级管理员可以修改管理员密码"));
    }

    var form = await context.Request.ReadFormAsync();
    if (!long.TryParse(form["id"].ToString(), out var id))
    {
        return Results.Redirect("/admin?error=" + Uri.EscapeDataString("管理员不存在"));
    }

    var password = form["password"].ToString();
    if (password.Length is < 8 or > 128)
    {
        return Results.Redirect("/admin?error=" + Uri.EscapeDataString("密码至少 8 位"));
    }

    if (database.AdminById(id) is null || !database.SetAdminPassword(id, password))
    {
        return Results.Redirect("/admin?error=" + Uri.EscapeDataString("管理员不存在"));
    }

    return Results.Redirect("/admin");
});

app.MapPost("/admin/admins/delete", async (HttpContext context) =>
{
    var current = CurrentAdmin(context);
    if (current is null)
    {
        return Results.Redirect("/admin");
    }

    if (!current.IsSuper)
    {
        return Results.Redirect("/admin?error=" + Uri.EscapeDataString("只有超级管理员可以删除管理员"));
    }

    var form = await context.Request.ReadFormAsync();
    if (!long.TryParse(form["id"].ToString(), out var id))
    {
        return Results.Redirect("/admin?error=" + Uri.EscapeDataString("管理员不存在"));
    }

    var target = database.AdminById(id);
    if (target is null)
    {
        return Results.Redirect("/admin?error=" + Uri.EscapeDataString("管理员不存在"));
    }

    if (target.IsSuper)
    {
        return Results.Redirect("/admin?error=" + Uri.EscapeDataString("超级管理员不能被删除"));
    }

    if (!database.DeleteAdmin(id))
    {
        return Results.Redirect("/admin?error=" + Uri.EscapeDataString("删除管理员失败"));
    }

    return Results.Redirect("/admin");
});

app.Map("/v1/ws", async context =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsync("websocket required");
        return;
    }

    var query = context.Request.Query;
    var room = Clean(query["room"], "demo").ToLowerInvariant();
    var name = Clean(query["name"], "匿名");
    var clientId = Clean(query["client"], string.Empty);
    var device = Clean(query["device"], "windows");
    var role = Clean(query["role"], "member");

    if (database.GetRoom(room) is null)
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        await context.Response.WriteAsync("room_not_found");
        return;
    }

    using var socket = await context.WebSockets.AcceptWebSocketAsync();
    var result = registry.Connect(room, name, device, role, socket, clientId);
    var member = result.Member;
    var connectionId = member.ConnectionId;
    database.TouchRoom(room);

    if (result.ExpiredMember is { } expired)
    {
        app.Logger.LogInformation(
            "event=expired-left room={Room} clientId={ClientId} memberId={MemberId} connectionId={ConnectionId} reason=expired-on-connect",
            room,
            expired.ClientId,
            expired.Id,
            expired.ConnectionId);
        await registry.BroadcastLeftAsync(expired);
        await RoomRegistry.CloseSocketAsync(expired.Socket, "reconnect expired");
    }

    if (result.Resumed)
    {
        app.Logger.LogInformation(
            "event=resume room={Room} clientId={ClientId} memberId={MemberId} connectionId={ConnectionId}",
            room,
            member.ClientId,
            member.Id,
            connectionId);
        if (result.PreviousSocket is { } previousSocket)
        {
            await RoomRegistry.CloseSocketAsync(previousSocket, "replaced by reconnect");
        }
    }
    else
    {
        app.Logger.LogInformation(
            "event=join room={Room} clientId={ClientId} memberId={MemberId} connectionId={ConnectionId} name={Name} device={Device}",
            room,
            member.ClientId,
            member.Id,
            connectionId,
            name,
            device);
    }

    try
    {
        await registry.SendWelcomeAsync(member);
        await registry.SendSnapshotAsync(member);
        await registry.SendRoomAvatarsAsync(member);
        await registry.BroadcastMemberAsync(member, includeSelf: false);
        if (!result.Resumed)
        {
            await registry.BroadcastJoinedAsync(member);
        }

        await registry.PumpAsync(member, socket, connectionId, context.RequestAborted);
    }
    catch (Exception ex) when (ex is WebSocketException or OperationCanceledException)
    {
        // client went away; fall through to cleanup
    }
    finally
    {
        await registry.DisconnectAsync(member, connectionId);
    }
});

_ = Task.Run(async () =>
{
    var sweepMs = 5_000;
    if (int.TryParse(
            Environment.GetEnvironmentVariable("NINEMROOM_SWEEP_MS"),
            out var configuredSweep) &&
        configuredSweep >= 100)
    {
        sweepMs = configuredSweep;
    }

    while (!app.Lifetime.ApplicationStopping.IsCancellationRequested)
    {
        try
        {
            await Task.Delay(TimeSpan.FromMilliseconds(sweepMs), app.Lifetime.ApplicationStopping);
        }
        catch (OperationCanceledException)
        {
            break;
        }

        await registry.SweepAsync();
    }
});

app.Run();

static string Clean(Microsoft.Extensions.Primitives.StringValues value, string fallback)
{
    var text = value.ToString().Trim();
    if (string.IsNullOrEmpty(text))
    {
        return fallback;
    }

    return text.Length <= 32 ? text : text[..32];
}
