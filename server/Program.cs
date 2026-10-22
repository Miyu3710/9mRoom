using System.Net.WebSockets;
using System.Security.Cryptography;
using NineMRoom.Server;

const string AdminCookie = "nm_admin";

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
var registry = new RoomRegistry();
app.Logger.LogInformation("database={Path}", databasePath);

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

    database.CreateAdmin(username, password!);
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
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/healthz", () => Results.Ok(new
{
    ok = true,
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

    var previous = registry.FindByClientId(room, clientId);
    if (previous is not null)
    {
        registry.Remove(previous);
        await registry.BroadcastLeftAsync(previous);
        try
        {
            await previous.Socket.CloseAsync(
                WebSocketCloseStatus.PolicyViolation,
                "replaced",
                CancellationToken.None);
        }
        catch
        {
            // 旧连接可能已经断开
        }
    }

    using var socket = await context.WebSockets.AcceptWebSocketAsync();
    var member = registry.Add(room, name, device, role, socket, clientId);
    database.TouchRoom(room);
    app.Logger.LogInformation("join room={Room} name={Name} id={Id} device={Device}", room, name, member.Id, device);

    try
    {
        await registry.SendWelcomeAsync(member);
        await registry.SendSnapshotAsync(member);
        await registry.BroadcastMemberAsync(member, includeSelf: false);
        await registry.BroadcastJoinedAsync(member);
        await registry.PumpAsync(member, context.RequestAborted);
    }
    catch (Exception ex) when (ex is WebSocketException or OperationCanceledException)
    {
        // client went away; fall through to cleanup
    }
    finally
    {
        registry.Remove(member);
        await registry.BroadcastLeftAsync(member);
        app.Logger.LogInformation("leave room={Room} name={Name} id={Id}", room, name, member.Id);
    }
});

_ = Task.Run(async () =>
{
    while (!app.Lifetime.ApplicationStopping.IsCancellationRequested)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(5), app.Lifetime.ApplicationStopping);
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
