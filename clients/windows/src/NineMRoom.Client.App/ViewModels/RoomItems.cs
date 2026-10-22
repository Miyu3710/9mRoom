namespace NineMRoom.Client.App;

public sealed class RoomLogItem
{
    public RoomLogItem(string time, string text, string kind)
    {
        Time = time;
        Text = text;
        Kind = kind;
    }

    public string Time { get; }

    public string Text { get; }

    public string Kind { get; }
}

public sealed class ChatMessageItem
{
    public ChatMessageItem(string id, string name, string device, string text, string time, bool isOwn)
    {
        Id = id;
        Name = name;
        Device = device;
        Text = text;
        Time = time;
        IsOwn = isOwn;
    }

    public string Id { get; }

    public string Name { get; }

    public string Device { get; }

    public string Text { get; }

    public string Time { get; }

    public bool IsOwn { get; }
}
