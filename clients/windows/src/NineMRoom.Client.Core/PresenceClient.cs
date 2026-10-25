using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace NineMRoom.Client.Core;

public sealed record RoomMember(
    string Id,
    string Name,
    string Device,
    string Role,
    bool Online,
    string Mode,
    long UpdatedAt,
    JsonElement? Status,
    string WindowTitle,
    long FocusSecondsToday,
    string ConnectionState);

public sealed record ChatMessage(
    string Id,
    string MemberId,
    string Name,
    string Device,
    string Text,
    long At);

public sealed record PokeFrom(
    string Id,
    string Name,
    string Device,
    string Mode);

public sealed record PokeReceived(
    PokeFrom From,
    string TargetId,
    long At);

public sealed class PresenceClient : IAsyncDisposable
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly Uri _uri;
    private readonly Func<object> _payloadFactory;
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private readonly ConcurrentDictionary<string, RoomMember> _members = new();

    private ClientWebSocket? _socket;
    private CancellationTokenSource? _cts;
    private bool _disposed;

    public PresenceClient(Uri uri, Func<object> payloadFactory)
    {
        _uri = uri;
        _payloadFactory = payloadFactory;
    }

    public bool IsConnected => _socket?.State == WebSocketState.Open;

    public int OnlineCount => _members.Values.Count(member => member.Online);

    public int MemberCount => _members.Count;

    public string? SelfId { get; private set; }

    public long LastSendAt { get; private set; }

    public bool Paused { get; set; }

    public TimeSpan SendInterval { get; set; } = TimeSpan.FromSeconds(3);

    public IReadOnlyList<RoomMember> Members => _members.Values
        .OrderByDescending(member => member.Online)
        .ThenBy(member => member.Name, StringComparer.CurrentCulture)
        .ToArray();

    public event Action? StateChanged;

    public event Action? MembersChanged;

    public event Action<string>? Message;

    public event Action<ChatMessage>? ChatReceived;

    public event Action<PokeReceived>? PokeReceived;

    public async Task ConnectAsync(CancellationToken cancellationToken)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _socket = new ClientWebSocket();
        await _socket.ConnectAsync(_uri, cancellationToken);
        StateChanged?.Invoke();
        _ = Task.Run(() => SendLoopAsync(_cts.Token));
        _ = Task.Run(() => ReceiveLoopAsync(_cts.Token));
    }

    public async Task SendNowAsync()
    {
        var socket = _socket;
        if (socket is null || socket.State != WebSocketState.Open)
        {
            return;
        }

        var payload = new
        {
            v = 1,
            type = "presence.update",
            id = Guid.NewGuid().ToString("n")[..8],
            ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            payload = _payloadFactory()
        };

        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload, Json));
        await _sendLock.WaitAsync();
        try
        {
            await socket.SendAsync(bytes, WebSocketMessageType.Text, endOfMessage: true, CancellationToken.None);
            LastSendAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }
        finally
        {
            _sendLock.Release();
        }
    }

    public async Task UpdateNameAsync(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        await SendMessageAsync(new
        {
            v = 1,
            type = "profile.update",
            ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            payload = new { name = name.Trim() }
        });
    }

    public async Task SendChatAsync(string text)
    {
        var message = (text ?? string.Empty).Trim();
        if (message.Length == 0 || message.Length > 500)
        {
            return;
        }

        await SendMessageAsync(new
        {
            v = 1,
            type = "chat.send",
            ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            payload = new { text = message }
        });
    }

    public async Task SendPokeAsync(string targetId)
    {
        var id = (targetId ?? string.Empty).Trim();
        if (id.Length == 0)
        {
            return;
        }

        await SendMessageAsync(new
        {
            v = 1,
            type = "poke.send",
            ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            payload = new { targetId = id }
        });
    }

    public Task SendLeaveAsync() =>
        SendMessageAsync(new
        {
            v = 1,
            type = "leave",
            ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            payload = new { }
        });

    private async Task SendMessageAsync(object message)
    {
        var socket = _socket;
        if (socket is null || socket.State != WebSocketState.Open)
        {
            return;
        }

        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message, Json));
        await _sendLock.WaitAsync();
        try
        {
            await socket.SendAsync(bytes, WebSocketMessageType.Text, endOfMessage: true, CancellationToken.None);
        }
        finally
        {
            _sendLock.Release();
        }
    }

    private async Task SendLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && IsConnected)
        {
            if (!Paused)
            {
                try
                {
                    await SendNowAsync();
                }
                catch (Exception ex) when (ex is WebSocketException or ObjectDisposedException)
                {
                    break;
                }
            }

            var delay = SendInterval < TimeSpan.FromSeconds(1)
                ? TimeSpan.FromSeconds(1)
                : SendInterval;

            try
            {
                await Task.Delay(delay, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        StateChanged?.Invoke();
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[16 * 1024];
        try
        {
            while (!cancellationToken.IsCancellationRequested && _socket is { State: WebSocketState.Open })
            {
                using var message = new MemoryStream();
                WebSocketReceiveResult result;

                do
                {
                    result = await _socket.ReceiveAsync(buffer, cancellationToken);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        return;
                    }

                    message.Write(buffer, 0, result.Count);
                }
                while (!result.EndOfMessage);

                Handle(Encoding.UTF8.GetString(message.GetBuffer(), 0, (int)message.Length));
            }
        }
        catch (Exception ex) when (ex is WebSocketException or OperationCanceledException or ObjectDisposedException)
        {
            // 连接断开由 StateChanged 通知上层
        }
        finally
        {
            StateChanged?.Invoke();
        }
    }

    private void Handle(string text)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(text);
        }
        catch (JsonException)
        {
            return;
        }

        using (document)
        {
            var root = document.RootElement;
            if (!root.TryGetProperty("type", out var typeElement))
            {
                return;
            }

            switch (typeElement.GetString())
            {
                case "welcome":
                    if (root.TryGetProperty("payload", out var welcome) &&
                        welcome.TryGetProperty("userId", out var userId))
                    {
                        SelfId = userId.GetString();
                    }

                    break;

                case "room.snapshot":
                    _members.Clear();
                    if (root.TryGetProperty("payload", out var snapshot) &&
                        snapshot.TryGetProperty("members", out var members) &&
                        members.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var member in members.EnumerateArray())
                        {
                            var parsed = ParseMember(member);
                            if (parsed is not null)
                            {
                                _members[parsed.Id] = parsed;
                            }
                        }
                    }

                    MembersChanged?.Invoke();
                    break;

                case "presence.changed":
                    if (root.TryGetProperty("payload", out var changed) &&
                        changed.TryGetProperty("member", out var changedMember))
                    {
                        var parsed = ParseMember(changedMember);
                        if (parsed is not null)
                        {
                            _members[parsed.Id] = parsed;
                            MembersChanged?.Invoke();
                        }
                    }

                    break;

                case "member.joined":
                    if (root.TryGetProperty("payload", out var joined) &&
                        joined.TryGetProperty("member", out var joinedMember))
                    {
                        var parsed = ParseMember(joinedMember);
                        if (parsed is not null)
                        {
                            _members[parsed.Id] = parsed;
                            MembersChanged?.Invoke();
                        }
                    }

                    break;

                case "member.left":
                    if (root.TryGetProperty("payload", out var left) &&
                        left.TryGetProperty("id", out var leftId) &&
                        _members.TryRemove(leftId.GetString() ?? string.Empty, out _))
                    {
                        MembersChanged?.Invoke();
                    }

                    break;

                case "chat.message":
                    if (root.TryGetProperty("payload", out var chat) && chat.ValueKind == JsonValueKind.Object)
                    {
                        var id = chat.TryGetProperty("id", out var chatId) ? chatId.GetString() : null;
                        var memberId = chat.TryGetProperty("memberId", out var chatMember) ? chatMember.GetString() : null;
                        var chatBody = chat.TryGetProperty("text", out var chatText) ? chatText.GetString() : null;
                        if (!string.IsNullOrWhiteSpace(id) && !string.IsNullOrWhiteSpace(chatBody))
                        {
                            var at = chat.TryGetProperty("at", out var chatAt) &&
                                     chatAt.ValueKind == JsonValueKind.Number &&
                                     chatAt.TryGetInt64(out var value)
                                ? value
                                : DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                            ChatReceived?.Invoke(new ChatMessage(
                                id,
                                memberId ?? string.Empty,
                                chat.TryGetProperty("name", out var chatName) ? chatName.GetString() ?? string.Empty : string.Empty,
                                chat.TryGetProperty("device", out var chatDevice) ? chatDevice.GetString() ?? string.Empty : string.Empty,
                                chatBody,
                                at));
                        }
                    }

                    break;

                case "poke.received":
                    if (root.TryGetProperty("payload", out var poke) &&
                        poke.ValueKind == JsonValueKind.Object &&
                        poke.TryGetProperty("from", out var from) &&
                        from.ValueKind == JsonValueKind.Object)
                    {
                        var at = poke.TryGetProperty("at", out var pokeAt) &&
                                 pokeAt.ValueKind == JsonValueKind.Number &&
                                 pokeAt.TryGetInt64(out var pokeValue)
                            ? pokeValue
                            : DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                        PokeReceived?.Invoke(new PokeReceived(
                            new PokeFrom(
                                Str(from, "id"),
                                Str(from, "name"),
                                Str(from, "device"),
                                Str(from, "mode")),
                            Str(poke, "targetId"),
                            at));
                    }

                    break;

                case "error":
                    if (root.TryGetProperty("payload", out var error))
                    {
                        var code = error.TryGetProperty("code", out var c) ? c.GetString() : "error";
                        var message = error.TryGetProperty("message", out var m) ? m.GetString() : string.Empty;
                        Message?.Invoke($"{code}: {message}");
                    }

                    break;
            }
        }
    }

    private static RoomMember? ParseMember(JsonElement member)
    {
        if (member.ValueKind != JsonValueKind.Object || !member.TryGetProperty("id", out var idElement))
        {
            return null;
        }

        var id = idElement.GetString();
        if (string.IsNullOrEmpty(id))
        {
            return null;
        }

        var online = !member.TryGetProperty("online", out var onlineElement) ||
                     onlineElement.ValueKind != JsonValueKind.False;

        var updatedAt = member.TryGetProperty("updatedAt", out var updatedElement) &&
                        updatedElement.ValueKind == JsonValueKind.Number &&
                        updatedElement.TryGetInt64(out var updated)
            ? updated
            : 0;

        JsonElement? status = null;
        if (member.TryGetProperty("status", out var statusElement) && statusElement.ValueKind == JsonValueKind.Object)
        {
            status = statusElement.Clone();
        }

        var focusSeconds = member.TryGetProperty("focusSecondsToday", out var focusElement) &&
                          focusElement.ValueKind == JsonValueKind.Number &&
                          focusElement.TryGetInt64(out var focusValue)
            ? focusValue
            : 0;

        return new RoomMember(
            id,
            Str(member, "name"),
            Str(member, "device"),
            Str(member, "role"),
            online,
            Str(member, "mode"),
            updatedAt,
            status,
            Str(member, "windowTitle"),
            focusSeconds,
            string.IsNullOrWhiteSpace(Str(member, "connectionState"))
                ? (online ? "online" : "offline")
                : Str(member, "connectionState"));
    }

    private static string Str(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _cts?.Cancel();

        if (_socket is { State: WebSocketState.Open })
        {
            try
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                await _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", timeout.Token);
            }
            catch
            {
                // 忽略关闭失败
            }
        }

        _socket?.Dispose();
        _cts?.Dispose();
        _sendLock.Dispose();
    }
}
