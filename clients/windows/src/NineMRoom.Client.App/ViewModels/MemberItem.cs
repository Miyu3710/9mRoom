namespace NineMRoom.Client.App;

public sealed class MemberItem : ObservableObject
{
    private string _name = string.Empty;
    private string _deviceLabel = string.Empty;
    private string _initial = "?";
    private string _statusText = string.Empty;
    private string _agoText = string.Empty;
    private string _windowTitle = string.Empty;
    private string _modeText = string.Empty;
    private string _todayFocusText = string.Empty;
    private bool _isOnline = true;
    private bool _canPoke;
    private string _connectionState = "online";

    public string Id { get; init; } = string.Empty;

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public string DeviceLabel
    {
        get => _deviceLabel;
        set => SetProperty(ref _deviceLabel, value);
    }

    public string Initial
    {
        get => _initial;
        set => SetProperty(ref _initial, value);
    }

    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    public string AgoText
    {
        get => _agoText;
        set => SetProperty(ref _agoText, value);
    }

    public string WindowTitle
    {
        get => _windowTitle;
        set
        {
            if (SetProperty(ref _windowTitle, value))
            {
                OnPropertyChanged(nameof(HasWindowTitle));
            }
        }
    }

    public string ModeText
    {
        get => _modeText;
        set => SetProperty(ref _modeText, value);
    }

    public string TodayFocusText
    {
        get => _todayFocusText;
        set => SetProperty(ref _todayFocusText, value);
    }

    public bool HasWindowTitle => !string.IsNullOrWhiteSpace(_windowTitle);

    public bool CanPoke
    {
        get => _canPoke;
        set => SetProperty(ref _canPoke, value);
    }

    public bool IsOnline
    {
        get => _isOnline;
        set
        {
            if (SetProperty(ref _isOnline, value))
            {
                OnPropertyChanged(nameof(PresenceLabel));
            }
        }
    }

    public string ConnectionState
    {
        get => _connectionState;
        set
        {
            if (SetProperty(ref _connectionState, value ?? "online"))
            {
                OnPropertyChanged(nameof(IsReconnecting));
                OnPropertyChanged(nameof(PresenceLabel));
            }
        }
    }

    public bool IsReconnecting => string.Equals(_connectionState, "reconnecting", StringComparison.Ordinal);

    public string PresenceLabel => IsReconnecting ? "重连中" : (IsOnline ? string.Empty : "离线");
}
