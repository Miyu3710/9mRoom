using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace NineMRoom.Server;

public sealed class Member
{
    public required string Id { get; init; }
    public string ClientId { get; init; } = string.Empty;
    public required string Name { get; set; }
    public required string Device { get; init; }
    public required string Role { get; init; }
    public required string RoomId { get; init; }
    public required WebSocket Socket { get; set; }

    public JsonElement? Status { get; set; }
    public string? WindowTitle { get; set; }
    public long FocusSecondsToday { get; set; }
    public string Mode { get; set; } = "idle";
    public bool Online { get; set; } = true;
    public long ConnectionId { get; set; }
    public string ConnectionState { get; set; } = "online";
    public bool ExplicitLeave { get; set; }
    public long UpdatedAt { get; set; }
    public long ExpiresAt { get; set; }
    public long LastChatAt { get; set; }
    public long LastPokeAt { get; set; }
    public SemaphoreSlim SendLock { get; } = new(1, 1);
}

internal sealed class Room
{
    public string Kind { get; init; } = "user";
    public ConcurrentDictionary<string, Member> Members { get; } = new();
    public object SyncRoot { get; } = new();
}

public sealed record ConnectResult(
    Member Member,
    bool Resumed,
    WebSocket? PreviousSocket,
    Member? ExpiredMember);

public sealed class RoomRegistry
{
    private const int MaxMessageBytes = 32 * 1024;
    private const long DefaultReconnectGraceMs = 120_000;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly ConcurrentDictionary<string, Room> _rooms = new();
    private readonly long _reconnectGraceMs;
    private readonly Action<string>? _log;

    public RoomRegistry(long reconnectGraceMs = DefaultReconnectGraceMs, Action<string>? log = null)
    {
        _reconnectGraceMs = Math.Max(1_000, reconnectGraceMs);
        _log = log;
    }

    public int RoomCount => _rooms.Count;
    public int MemberCount => _rooms.Values.Sum(room => room.Members.Count);

    public Member Add(string roomId, string name, string device, string role, WebSocket socket, string clientId = "")
    {
        var room = _rooms.GetOrAdd(roomId, _ => new Room());
        lock (room.SyncRoot)
        {
            var member = CreateMember(roomId, name, device, role, socket, clientId, Now());
            room.Members[member.Id] = member;
            return member;
        }
    }

    public ConnectResult Connect(
        string roomId,
        string name,
        string device,
        string role,
        WebSocket socket,
        string clientId = "")
    {
        var room = _rooms.GetOrAdd(roomId, _ => new Room());
        lock (room.SyncRoot)
        {
            var now = Now();
            var existing = string.IsNullOrWhiteSpace(clientId)
                ? null
                : room.Members.Values.FirstOrDefault(member =>
                    string.Equals(member.ClientId, clientId, StringComparison.Ordinal));

            if (existing is not null && now <= existing.ExpiresAt)
            {
                var previousSocket = existing.Socket;
                existing.Name = name;
                existing.Socket = socket;
                existing.ConnectionId++;
                existing.Online = true;
                existing.ConnectionState = "online";
                existing.ExplicitLeave = false;
                existing.UpdatedAt = now;
                existing.ExpiresAt = now + _reconnectGraceMs;
                return new ConnectResult(existing, true, previousSocket, null);
            }

            Member? expired = null;
            if (existing is not null)
            {
                room.Members.TryRemove(existing.Id, out _);
                expired = existing;
            }

            var member = CreateMember(roomId, name, device, role, socket, clientId, now);
            room.Members[member.Id] = member;
            return new ConnectResult(member, false, null, expired);
        }
    }

    private Member CreateMember(
        string roomId,
        string name,
        string device,
        string role,
        WebSocket socket,
        string clientId,
        long now) => new()
    {
        Id = "u_" + Guid.NewGuid().ToString("n")[..8],
        ClientId = clientId,
        Name = name,
        Device = device,
        Role = role,
        RoomId = roomId,
        Socket = socket,
        ConnectionId = 1,
        ConnectionState = "online",
        UpdatedAt = now,
        ExpiresAt = now + _reconnectGraceMs
    };

    public Member? FindByClientId(string roomId, string clientId)
    {
        if (string.IsNullOrWhiteSpace(clientId) ||
            !_rooms.TryGetValue(roomId, out var room))
        {
            return null;
        }

        return room.Members.Values.FirstOrDefault(member =>
            string.Equals(member.ClientId, clientId, StringComparison.Ordinal));
    }

    public void EnsureRoom(string roomId, string kind)
    {
        _rooms.GetOrAdd(roomId, _ => new Room { Kind = kind });
    }

    public object RoomInfo(string roomId)
    {
        if (_rooms.TryGetValue(roomId, out var room))
        {
            return new
            {
                code = roomId,
                exists = true,
                kind = room.Kind,
                online = room.Members.Values.Count(member => member.Online)
            };
        }

        return new
        {
            code = roomId,
            exists = false,
            kind = "user",
            online = 0
        };
    }

    public int OnlineCount(string roomId) =>
        _rooms.TryGetValue(roomId, out var room)
            ? room.Members.Values.Count(member => member.Online)
            : 0;

    public async Task DropRoomAsync(string roomId)
    {
        if (!_rooms.TryRemove(roomId, out var room))
        {
            return;
        }

        foreach (var member in room.Members.Values.ToArray())
        {
            try
            {
                await member.Socket.CloseAsync(
                    WebSocketCloseStatus.PolicyViolation,
                    "room_closed",
                    CancellationToken.None);
            }
            catch
            {
                // 忽略关闭失败
            }
        }
    }

    public void Remove(Member member)
    {
        if (_rooms.TryGetValue(member.RoomId, out var room))
        {
            room.Members.TryRemove(member.Id, out _);
        }
    }

    public Task SendWelcomeAsync(Member member) => SendJsonAsync(member, Envelope("welcome", new
    {
        serverTime = Now(),
        userId = member.Id,
        roomId = member.RoomId,
        self = View(member)
    }));

    public Task SendSnapshotAsync(Member member)
    {
        var members = _rooms.TryGetValue(member.RoomId, out var room)
            ? room.Members.Values.Select(View).ToArray()
            : Array.Empty<object>();

        return SendJsonAsync(member, Envelope("room.snapshot", new
        {
            roomId = member.RoomId,
            serverTime = Now(),
            members
        }));
    }

    public async Task BroadcastMemberAsync(Member member, bool includeSelf)
    {
        var json = Envelope("presence.changed", new { member = View(member) });
        if (!_rooms.TryGetValue(member.RoomId, out var room))
        {
            return;
        }

        foreach (var other in room.Members.Values.ToArray())
        {
            if (!includeSelf && other.Id == member.Id)
            {
                continue;
            }

            await SendJsonAsync(other, json);
        }
    }

    public async Task BroadcastLeftAsync(Member member)
    {
        var json = Envelope("member.left", new { id = member.Id });
        if (!_rooms.TryGetValue(member.RoomId, out var room))
        {
            return;
        }

        foreach (var other in room.Members.Values.ToArray())
        {
            await SendJsonAsync(other, json);
        }
    }

    public async Task BroadcastJoinedAsync(Member member)
    {
        var json = Envelope("member.joined", new { member = View(member) });
        if (!_rooms.TryGetValue(member.RoomId, out var room))
        {
            return;
        }

        foreach (var other in room.Members.Values.ToArray())
        {
            if (other.Id != member.Id)
            {
                await SendJsonAsync(other, json);
            }
        }
    }

    public async Task BroadcastChatAsync(Member member, string text)
    {
        var json = Envelope("chat.message", new
        {
            id = Guid.NewGuid().ToString("n")[..12],
            memberId = member.Id,
            name = member.Name,
            device = member.Device,
            text,
            at = Now()
        });

        if (!_rooms.TryGetValue(member.RoomId, out var room))
        {
            return;
        }

        foreach (var other in room.Members.Values.ToArray())
        {
            await SendJsonAsync(other, json);
        }
    }

    public object? DebugRoom(string roomId)
    {
        if (!_rooms.TryGetValue(roomId, out var room))
        {
            return null;
        }

        return new
        {
            roomId,
            members = room.Members.Values.Select(member => new
            {
                member.Id,
                member.Name,
                member.Device,
                member.Role,
                member.Online,
                member.ConnectionState,
                member.Mode,
                member.UpdatedAt,
                status = member.Status,
                windowTitle = member.WindowTitle,
                focusSecondsToday = member.FocusSecondsToday
            }).ToArray()
        };
    }

    public bool IsCurrent(Member member, long connectionId)
    {
        if (!_rooms.TryGetValue(member.RoomId, out var room))
        {
            return false;
        }

        lock (room.SyncRoot)
        {
            return room.Members.TryGetValue(member.Id, out var current) &&
                   ReferenceEquals(current, member) &&
                   member.ConnectionId == connectionId &&
                   !member.ExplicitLeave;
        }
    }

    public async Task DisconnectAsync(Member member, long connectionId)
    {
        if (!_rooms.TryGetValue(member.RoomId, out var room))
        {
            return;
        }

        lock (room.SyncRoot)
        {
            if (!room.Members.TryGetValue(member.Id, out var current) ||
                !ReferenceEquals(current, member) ||
                member.ConnectionId != connectionId ||
                member.ExplicitLeave ||
                !member.Online)
            {
                return;
            }

            member.Online = false;
            member.ConnectionState = "reconnecting";
            member.UpdatedAt = Now();
            member.ExpiresAt = member.UpdatedAt + _reconnectGraceMs;
        }

        Log($"event=reconnecting room={member.RoomId} clientId={member.ClientId} memberId={member.Id} connectionId={connectionId}");
        await BroadcastMemberAsync(member, includeSelf: false);
    }

    public async Task LeaveExplicitAsync(Member member, long connectionId)
    {
        if (!_rooms.TryGetValue(member.RoomId, out var room))
        {
            return;
        }

        lock (room.SyncRoot)
        {
            if (!room.Members.TryGetValue(member.Id, out var current) ||
                !ReferenceEquals(current, member) ||
                member.ConnectionId != connectionId ||
                member.ExplicitLeave)
            {
                return;
            }

            room.Members.TryRemove(member.Id, out _);
            member.Online = false;
            member.ConnectionState = "offline";
            member.ExplicitLeave = true;
            member.UpdatedAt = Now();
        }

        Log($"event=explicit-leave room={member.RoomId} clientId={member.ClientId} memberId={member.Id} connectionId={connectionId}");
        await BroadcastLeftAsync(member);
    }

    public static async Task CloseSocketAsync(WebSocket socket, string reason)
    {
        try
        {
            if (socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, reason, cts.Token);
            }
        }
        catch
        {
            try
            {
                socket.Abort();
            }
            catch
            {
                // 忽略关闭失败
            }
        }
    }

    public async Task SweepAsync()
    {
        var now = Now();
        foreach (var room in _rooms.Values)
        {
            foreach (var member in room.Members.Values.ToArray())
            {
                var reconnecting = false;
                var expired = false;

                lock (room.SyncRoot)
                {
                    if (!room.Members.TryGetValue(member.Id, out var current) ||
                        !ReferenceEquals(current, member) ||
                        now <= member.ExpiresAt)
                    {
                        continue;
                    }

                    if (member.Online)
                    {
                        member.Online = false;
                        member.ConnectionState = "reconnecting";
                        member.UpdatedAt = now;
                        member.ExpiresAt = now + _reconnectGraceMs;
                        reconnecting = true;
                    }
                    else
                    {
                        if (!room.Members.TryRemove(member.Id, out _))
                        {
                            continue;
                        }

                        member.ConnectionState = "offline";
                        member.UpdatedAt = now;
                        expired = true;
                    }
                }

                if (reconnecting)
                {
                    Log($"event=reconnecting room={member.RoomId} clientId={member.ClientId} memberId={member.Id} connectionId={member.ConnectionId}");
                    await BroadcastMemberAsync(member, includeSelf: false);
                }
                else if (expired)
                {
                    Log($"event=expired-left room={member.RoomId} clientId={member.ClientId} memberId={member.Id} connectionId={member.ConnectionId}");
                    await BroadcastLeftAsync(member);
                    await CloseSocketAsync(member.Socket, "reconnect expired");
                }
            }
        }
    }

    public async Task PumpAsync(Member member, WebSocket socket, long connectionId, CancellationToken cancellationToken)
    {
        var buffer = new byte[16 * 1024];
        using var accumulator = new MemoryStream();

        while (!cancellationToken.IsCancellationRequested && socket.State == WebSocketState.Open)
        {
            WebSocketReceiveResult result;
            try
            {
                result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
            }
            catch (Exception ex) when (ex is WebSocketException or OperationCanceledException)
            {
                break;
            }

            if (!IsCurrent(member, connectionId))
            {
                break;
            }

            if (result.MessageType == WebSocketMessageType.Close)
            {
                break;
            }

            if (result.MessageType != WebSocketMessageType.Text)
            {
                await CloseSocketAsync(socket, "text frames only");
                break;
            }

            accumulator.Write(buffer, 0, result.Count);
            if (accumulator.Length > MaxMessageBytes)
            {
                await CloseSocketAsync(socket, "message too large");
                break;
            }

            if (!result.EndOfMessage)
            {
                continue;
            }

            var text = Encoding.UTF8.GetString(accumulator.GetBuffer(), 0, (int)accumulator.Length);
            accumulator.SetLength(0);
            if (!IsCurrent(member, connectionId))
            {
                break;
            }

            if (await HandleAsync(member, connectionId, text))
            {
                break;
            }
        }
    }

    private async Task<bool> HandleAsync(Member member, long connectionId, string text)
    {
        if (!IsCurrent(member, connectionId))
        {
            return false;
        }
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(text);
        }
        catch (JsonException)
        {
            await SendErrorAsync(member, "bad_json", "message is not valid json");
            return false;
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("type", out var typeElement) ||
                typeElement.ValueKind != JsonValueKind.String)
            {
                await SendErrorAsync(member, "bad_envelope", "type is required");
                return false;
            }

            switch (typeElement.GetString())
            {
                case "ping":
                    await SendJsonAsync(member, Envelope("pong", new { serverTime = Now() }));
                    break;

                case "presence.update":
                    await HandlePresenceAsync(member, connectionId, root);
                    break;

                case "profile.update":
                    await HandleProfileUpdateAsync(member, connectionId, root);
                    break;

                case "chat.send":
                    await HandleChatAsync(member, connectionId, root);
                    break;

                case "poke.send":
                    await HandlePokeAsync(member, connectionId, root);
                    break;

                case "leave":
                    await LeaveExplicitAsync(member, connectionId);
                    return true;

                case "hello":
                    await SendJsonAsync(member, Envelope("welcome", new
                    {
                        serverTime = Now(),
                        userId = member.Id,
                        roomId = member.RoomId,
                        self = View(member)
                    }));
                    break;

                default:
                    await SendErrorAsync(member, "unknown_type", "unsupported message type");
                    break;
            }
        }

        return false;
    }

    private async Task HandleChatAsync(Member member, long connectionId, JsonElement root)
    {
        if (!IsCurrent(member, connectionId))
        {
            return;
        }
        if (!root.TryGetProperty("payload", out var payload) ||
            payload.ValueKind != JsonValueKind.Object ||
            !payload.TryGetProperty("text", out var textElement) ||
            textElement.ValueKind != JsonValueKind.String)
        {
            await SendErrorAsync(member, "bad_chat", "text is required");
            return;
        }

        var text = (textElement.GetString() ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            await SendErrorAsync(member, "bad_chat", "text is empty");
            return;
        }

        if (text.Length > 500)
        {
            await SendErrorAsync(member, "bad_chat", "text is too long");
            return;
        }

        var now = Now();
        if (now - member.LastChatAt < 800)
        {
            await SendErrorAsync(member, "slow_down", "sending too fast");
            return;
        }

        member.LastChatAt = now;
        await BroadcastChatAsync(member, text);
    }

    private async Task HandlePokeAsync(Member member, long connectionId, JsonElement root)
    {
        if (!IsCurrent(member, connectionId))
        {
            return;
        }

        if (!root.TryGetProperty("payload", out var payload) ||
            payload.ValueKind != JsonValueKind.Object ||
            !payload.TryGetProperty("targetId", out var targetElement) ||
            targetElement.ValueKind != JsonValueKind.String)
        {
            await SendErrorAsync(member, "bad_poke", "targetId is required");
            return;
        }

        var targetId = (targetElement.GetString() ?? string.Empty).Trim();
        if (targetId.Length == 0 || targetId.Length > 64)
        {
            await SendErrorAsync(member, "bad_poke", "targetId is invalid");
            return;
        }

        if (string.Equals(targetId, member.Id, StringComparison.Ordinal))
        {
            await SendErrorAsync(member, "poke_self", "cannot poke yourself");
            return;
        }

        if (!_rooms.TryGetValue(member.RoomId, out var room) ||
            !room.Members.TryGetValue(targetId, out var target) ||
            !target.Online ||
            target.Socket.State != WebSocketState.Open)
        {
            await SendErrorAsync(member, "poke_not_found", "member is not online in this room");
            return;
        }

        var now = Now();
        if (now - member.LastPokeAt < 2000)
        {
            await SendErrorAsync(member, "slow_down", "poking too fast");
            return;
        }

        member.LastPokeAt = now;
        await SendJsonAsync(target, Envelope("poke.received", new
        {
            from = View(member),
            targetId = target.Id,
            at = now
        }));
        await SendJsonAsync(member, Envelope("poke.sent", new
        {
            targetId = target.Id,
            targetName = target.Name,
            at = now
        }));
    }

    private async Task HandleProfileUpdateAsync(Member member, long connectionId, JsonElement root)
    {
        if (!IsCurrent(member, connectionId))
        {
            return;
        }

        if (!root.TryGetProperty("payload", out var payload) ||
            payload.ValueKind != JsonValueKind.Object ||
            !payload.TryGetProperty("name", out var nameElement) ||
            nameElement.ValueKind != JsonValueKind.String)
        {
            await SendErrorAsync(member, "bad_name", "name is required");
            return;
        }

        var name = (nameElement.GetString() ?? string.Empty).Trim();
        if (name.Length is < 1 or > 24)
        {
            await SendErrorAsync(member, "bad_name", "name must be 1-24 characters");
            return;
        }

        member.Name = name;
        member.UpdatedAt = Now();
        member.ExpiresAt = member.UpdatedAt + _reconnectGraceMs;
        await BroadcastMemberAsync(member, includeSelf: true);
    }

    private async Task HandlePresenceAsync(Member member, long connectionId, JsonElement root)
    {
        if (!IsCurrent(member, connectionId))
        {
            return;
        }

        if (!root.TryGetProperty("payload", out var payload) || payload.ValueKind != JsonValueKind.Object)
        {
            await SendErrorAsync(member, "bad_payload", "payload object is required");
            return;
        }

        if (payload.TryGetProperty("status", out var status))
        {
            var problem = ValidateStatus(status);
            if (problem is not null)
            {
                await SendErrorAsync(member, "bad_status", problem);
                return;
            }

            member.Status = status.Clone();
        }

        if (payload.TryGetProperty("mode", out var modeElement) && modeElement.ValueKind == JsonValueKind.String)
        {
            member.Mode = modeElement.GetString() ?? member.Mode;
        }

        if (payload.TryGetProperty("windowTitle", out var titleElement) && titleElement.ValueKind == JsonValueKind.String)
        {
            var title = titleElement.GetString();
            member.WindowTitle = string.IsNullOrWhiteSpace(title) || title.Length > 200 ? null : title;
        }
        else
        {
            member.WindowTitle = null;
        }

        if (payload.TryGetProperty("focusSecondsToday", out var focusElement) &&
            focusElement.ValueKind == JsonValueKind.Number &&
            focusElement.TryGetInt64(out var focusSeconds) &&
            focusSeconds is >= 0 and <= 86_400)
        {
            member.FocusSecondsToday = focusSeconds;
        }
        else
        {
            member.FocusSecondsToday = 0;
        }

        if (string.Equals(member.Mode, "invisible", StringComparison.Ordinal))
        {
            member.Status = null;
            member.WindowTitle = null;
            member.FocusSecondsToday = 0;
        }

        member.Online = true;
        member.ConnectionState = "online";
        member.UpdatedAt = Now();
        member.ExpiresAt = member.UpdatedAt + _reconnectGraceMs;
        await BroadcastMemberAsync(member, includeSelf: true);
    }

    private static string? ValidateStatus(JsonElement status)
    {
        if (status.ValueKind != JsonValueKind.Object)
        {
            return "status must be an object";
        }

        if (!status.TryGetProperty("blocks", out var blocks) || blocks.ValueKind != JsonValueKind.Array)
        {
            return "status.blocks must be an array";
        }

        if (blocks.GetArrayLength() > 8)
        {
            return "at most 8 blocks";
        }

        if (status.GetRawText().Length > 8 * 1024)
        {
            return "status too large";
        }

        foreach (var block in blocks.EnumerateArray())
        {
            if (block.ValueKind != JsonValueKind.Object)
            {
                return "block must be an object";
            }

            if (!block.TryGetProperty("type", out var type) || type.ValueKind != JsonValueKind.String)
            {
                return "block.type is required";
            }

            if (block.TryGetProperty("template", out var template) &&
                template.ValueKind == JsonValueKind.String &&
                (template.GetString()?.Length ?? 0) > 80)
            {
                return "block.template is longer than 80 characters";
            }
        }

        return null;
    }

    private Task SendErrorAsync(Member member, string code, string message) =>
        SendJsonAsync(member, Envelope("error", new { code, message }));

    private async Task SendJsonAsync(Member member, string json)
    {
        if (member.Socket.State != WebSocketState.Open)
        {
            return;
        }

        await member.SendLock.WaitAsync();
        try
        {
            await member.Socket.SendAsync(
                Encoding.UTF8.GetBytes(json),
                WebSocketMessageType.Text,
                endOfMessage: true,
                CancellationToken.None);
        }
        catch (Exception ex) when (ex is WebSocketException or ObjectDisposedException)
        {
            // peer is gone; cleanup happens in the request finally block
        }
        finally
        {
            member.SendLock.Release();
        }
    }

    private static object View(Member member) => new
    {
        id = member.Id,
        name = member.Name,
        device = member.Device,
        role = member.Role,
        online = member.Online,
        connectionState = member.ConnectionState,
        mode = member.Mode,
        status = member.Status,
        windowTitle = member.WindowTitle,
        focusSecondsToday = member.FocusSecondsToday,
        updatedAt = member.UpdatedAt
    };

    private void Log(string message) => _log?.Invoke(message);

    private static string Envelope(string type, object payload) => JsonSerializer.Serialize(new
    {
        v = 1,
        type,
        ts = Now(),
        payload
    }, Json);

    private static long Now() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
}
