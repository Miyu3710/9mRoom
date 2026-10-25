using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Xml.Linq;
using NineMRoom.Client.App.Controls;
using NineMRoom.Client.Core;
using WpfApplication = System.Windows.Application;

namespace NineMRoom.Client.App;

public sealed class MainViewModel : ObservableObject, IDisposable
{
    private readonly AppSettings _settings;
    private readonly AppCatalog _catalog;
    private readonly ForegroundWatcher _foreground;
    private readonly Dispatcher _dispatcher;
    private readonly Dictionary<string, MemberItem> _memberMap = new();
    private readonly Dictionary<string, ImageSource> _avatarImageCache = new(StringComparer.Ordinal);
    private readonly Dictionary<string, AppRuleItem> _appRuleMap = new(StringComparer.Ordinal);
    private readonly Dictionary<string, AppCategoryItem> _appCategoryMap = new(StringComparer.Ordinal);
    private readonly ScriptRunner _scriptRunner = new();
    private readonly Dictionary<string, (string Name, string Mode, bool Online)> _roomMemberLog = new(StringComparer.Ordinal);
    private bool _roomLogInitialized;
    private string _chatInput = string.Empty;
    private static readonly HashSet<string> HiddenAppIds = new(StringComparer.OrdinalIgnoreCase)
    {
        "unknown", "protected", "applicationframehost"
    };

    private MediaWatcher? _media;
    private PresenceClient? _client;
    private DispatcherTimer? _timer;
    private DispatcherTimer? _reconnectTimer;
    private long _bootTime;
    private bool _initialized;
    private bool _wantsConnection;
    private int _reconnectAttempt;
    private bool _isServerConnected;
    private bool _wasReconnecting;
    private bool _settingsToastEnabled;
    private string _connectionState = "offline";
    private AvatarPayload? _avatar;
    private ImageSource? _myAvatarImage;
    private bool _isJoinDialogOpen;
    private string _joinRoomCode = string.Empty;
    private string _joinRoomName = string.Empty;
    private string _joinRoomInfoText = string.Empty;
    private string _roomCodeNotice = string.Empty;
    private bool _isCheckingUpdate;
    private bool _isUpdateAvailable;
    private string _updateStatusText = string.Empty;
    private string _updateDownloadUrl = string.Empty;
    private bool _joinRememberName = true;
    private bool _joinSetHome;
    private bool _joinCanSetHome;
    private DispatcherTimer? _joinInfoDebounce;
    private string _joinedRoom = string.Empty;
    private string _joinedName = string.Empty;

    private string _myStatusText = "等待采集…";
    private string _myAppText = "-";
    private string _myMediaText = "-";
    private string _myIdleText = "-";
    private string _lastSyncText = "未同步";
    private string _trayText = "9mRoom";
    private string _overlayHeader = "9mRoom · 未连接";
    private string _persistentNoticeText = string.Empty;
    private bool _isConnecting;
    private string _manualMode = "auto";
    private bool _paused;
    private long _timerStartedAt;
    private int _focusMinutes = 25;
    private int _breakMinutes = 5;
    private bool _timerCycleEnabled;
    private string _timerPhase = "focus";
    private bool _timerRunning;
    private long _timerRemainingMs = 25 * 60_000L;
    private string _timerText = "25:00";
    private readonly DispatcherTimer _todoDebounce;
    private DispatcherTimer? _layoutDebounce;
    private DispatcherTimer? _nameDebounce;
    private OverlayTheme _customTheme = OverlayThemes.DefaultCustom;
    private string _noticeText = string.Empty;
    private bool _trayAvailable = true;
    private DispatcherTimer? _noticeTimer;
    private MemberItem? _selectedMember;
    private bool _memberDrawerOpen;
    private AppRuleItem? _selectedAppRule;
    private AppRuleDraft? _appDraft;
    private bool _isAppEditorOpen;
    private bool _isAppRuleDirty;
    private string _lastDetectedAppId = string.Empty;
    private string _appSearchText = string.Empty;
    private readonly Dictionary<string, long> _focusHistory = new();
    private long _statsAccumulatorMs;
    private long _lastStatsTickMs;
    private long _lastStatsPersistMs;
    private string _lastHistoryBuild = string.Empty;
    private string _commandOutput = string.Empty;
    private bool _commandRunning;
    private DispatcherTimer? _commandTimer;

    public MainViewModel(AppSettings settings)
    {
        _settings = settings;
        SettingsStore.Saved += OnSettingsSaved;
        _focusMinutes = Math.Clamp(settings.TimerFocusMinutes > 0 ? settings.TimerFocusMinutes : 25, 1, 600);
        _breakMinutes = Math.Clamp(settings.TimerBreakMinutes > 0 ? settings.TimerBreakMinutes : 5, 1, 600);
        _timerCycleEnabled = settings.TimerCycleEnabled;
        _timerPhase = "focus";
        _timerRemainingMs = _focusMinutes * 60_000L;
        _timerText = FormatRemaining(_timerRemainingMs);
        settings.CommandEnabled = false;
        if (string.IsNullOrWhiteSpace(settings.ClientId))
        {
            settings.ClientId = Guid.NewGuid().ToString("n");
            SettingsStore.Save(settings);
        }

        _dispatcher = WpfApplication.Current.Dispatcher;
        _avatar = AvatarStore.LoadCached();
        _myAvatarImage = AvatarStore.ToImage(_avatar, 128);
        _catalog = AppCatalog.Load(Path.Combine(AppContext.BaseDirectory, "catalog.json"));
        _customTheme = LoadCustomTheme(settings.OverlayThemeJson);
        _foreground = new ForegroundWatcher();
        AppRulesView = CollectionViewSource.GetDefaultView(AppRules);
        AppRulesView.Filter = FilterAppRule;
        AppRules.CollectionChanged += (_, _) =>
        {
            if (HasAppSearch)
            {
                OnPropertyChanged(nameof(IsAppFilterEmpty));
            }
        };

        _todoDebounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(700) };
        _todoDebounce.Tick += (_, _) =>
        {
            _todoDebounce.Stop();
            _ = _client?.SendNowAsync();
        };

        _layoutDebounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _layoutDebounce.Tick += (_, _) =>
        {
            _layoutDebounce.Stop();
            PersistLayout();
            PersistAppRules();
            _ = _client?.SendNowAsync();
        };

        _nameDebounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        _nameDebounce.Tick += (_, _) =>
        {
            _nameDebounce.Stop();
            var name = _settings.Name.Trim();
            if (name.Length is >= 1 and <= 24)
            {
                _ = _client?.UpdateNameAsync(name);
            }
        };

        LoadLayout(settings.StatusLayoutJson);
        LoadAppRules(settings.AppRulesJson, settings.AppTemplatesJson);
        LoadAppCategories(settings.AppCategoriesJson);
        ApplyAppSort();
        LoadTodos(settings.TodosJson, settings.TodoText);
        LoadTitleWhitelist(settings.TitleWhitelistJson);
        LoadStats(settings.StatsHistoryJson);

        _commandTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        _commandTimer.Tick += async (_, _) => await RunCommandAsync();
        _commandTimer.Start();
        _ = RunCommandAsync();
    }

    public ObservableCollection<StatusBlockItem> LayoutBlocks { get; } = new();

    public ObservableCollection<AppRuleItem> AppRules { get; } = new();

    public ICollectionView AppRulesView { get; }

    public ObservableCollection<AppCategoryItem> AppCategories { get; } = new();

    public bool IsAppListEmpty => AppRules.Count == 0;

    public bool IsAppFilterEmpty => HasAppSearch && AppRulesView.IsEmpty;

    public bool HasAppSearch => !string.IsNullOrWhiteSpace(_appSearchText);

    public string AppSearchText
    {
        get => _appSearchText;
        set
        {
            var text = value ?? string.Empty;
            if (string.Equals(_appSearchText, text, StringComparison.Ordinal))
            {
                return;
            }

            _appSearchText = text;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasAppSearch));
            AppRulesView.Refresh();
            OnPropertyChanged(nameof(IsAppFilterEmpty));
        }
    }

    public void ClearAppSearch() => AppSearchText = string.Empty;

    private bool FilterAppRule(object item)
    {
        if (item is not AppRuleItem app)
        {
            return false;
        }

        var query = _appSearchText.Trim();
        if (query.Length == 0)
        {
            return true;
        }

        if (Contains(app.DisplayName) ||
            Contains(app.ProcessName) ||
            Contains(app.AppId) ||
            Contains(app.EffectiveCategory))
        {
            return true;
        }

        var category = AppCategories.FirstOrDefault(entry =>
            string.Equals(entry.Id, app.EffectiveCategory, StringComparison.OrdinalIgnoreCase));
        if (category is not null && Contains(category.Name))
        {
            return true;
        }

        return Contains(StatusLayouts.Label(app.EffectiveCategory));

        bool Contains(string? value) =>
            !string.IsNullOrEmpty(value) &&
            value.Contains(query, StringComparison.CurrentCultureIgnoreCase);
    }

    public string AppSortMode
    {
        get => _settings.AppSortMode;
        set
        {
            var mode = value is "name" or "category" ? value : "recent";
            if (string.Equals(_settings.AppSortMode, mode, StringComparison.Ordinal))
            {
                return;
            }

            _settings.AppSortMode = mode;
            SettingsStore.Save(_settings);
            OnPropertyChanged();
            OnPropertyChanged(nameof(AppSortLabel));
            ApplyAppSort();
        }
    }

    public string AppSortLabel => AppSortMode switch
    {
        "name" => "名称",
        "category" => "分类",
        _ => "最近使用"
    };

    private void ApplyAppSort()
    {
        var sorted = AppSortMode switch
        {
            "name" => AppRules
                .OrderBy(item => item.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                .ToList(),
            "category" => AppRules
                .OrderBy(item => item.EffectiveCategory, StringComparer.Ordinal)
                .ThenBy(item => item.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                .ToList(),
            _ => AppRules
                .OrderByDescending(item => item.LastSeenAt)
                .ThenBy(item => item.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                .ToList()
        };

        for (var i = 0; i < sorted.Count; i++)
        {
            var currentIndex = AppRules.IndexOf(sorted[i]);
            if (currentIndex != i)
            {
                AppRules.Move(currentIndex, i);
            }
        }
    }

    public ObservableCollection<TitleWhitelistItem> TitleWhitelist { get; } = new();

    public ObservableCollection<FocusDayItem> FocusHistory { get; } = new();

    public ObservableCollection<MemberItem> Members { get; } = new();

    public string ServerBase
    {
        get => _settings.ServerBase;
        set
        {
            if (string.Equals(_settings.ServerBase, value, StringComparison.Ordinal))
            {
                return;
            }

            _settings.ServerBase = value;
            SettingsStore.Save(_settings);
            OnPropertyChanged();
        }
    }

    public string Room
    {
        get => _settings.Room;
        set
        {
            if (string.Equals(_settings.Room, value, StringComparison.Ordinal))
            {
                return;
            }

            _settings.Room = value;
            SettingsStore.Save(_settings);
            OnPropertyChanged();
            OnPropertyChanged(nameof(RoomDisplayName));
        }
    }

    public string RoomDisplayName =>
        string.IsNullOrWhiteSpace(_settings.RoomName) ? _settings.Room : _settings.RoomName;

    public string RoomCodeNotice => _roomCodeNotice;

    public bool HasRoomCodeNotice => !string.IsNullOrWhiteSpace(_roomCodeNotice);

    public string RoomCodeDisplayText =>
        HasRoomCodeNotice ? _roomCodeNotice : "点击房间名复制房间码";

    public void ShowRoomCodeNotice(string text)
    {
        var value = text ?? string.Empty;
        if (string.Equals(_roomCodeNotice, value, StringComparison.Ordinal))
        {
            return;
        }

        _roomCodeNotice = value;
        OnPropertyChanged(nameof(RoomCodeNotice));
        OnPropertyChanged(nameof(HasRoomCodeNotice));
        OnPropertyChanged(nameof(RoomCodeDisplayText));
    }

    private void SetRoomName(string name)
    {
        var value = string.IsNullOrWhiteSpace(name) ? _settings.Room : name.Trim();
        if (string.Equals(_settings.RoomName, value, StringComparison.Ordinal))
        {
            return;
        }

        _settings.RoomName = value;
        SettingsStore.Save(_settings);
        OnPropertyChanged(nameof(RoomDisplayName));
    }

    public string Name
    {
        get => _settings.Name;
        set
        {
            if (string.Equals(_settings.Name, value, StringComparison.Ordinal))
            {
                return;
            }

            _settings.Name = value;
            SettingsStore.Save(_settings);
            OnPropertyChanged();
            OnPropertyChanged(nameof(MyAvatarInitial));

            // 构造函数里已经创建该定时器；这里只是重启防抖。
            _nameDebounce!.Stop();
            _nameDebounce.Start();
        }
    }

    public bool AutoStart
    {
        get => _settings.AutoStart;
        set
        {
            if (_settings.AutoStart == value)
            {
                return;
            }

            if (value && !AutoStartService.Set(true))
            {
                OnPropertyChanged();
                ShowNotice("开机自启设置失败：系统拒绝写入启动项，请检查权限。");
                return;
            }

            if (!value)
            {
                AutoStartService.Set(false);
            }

            _settings.AutoStart = value;
            SettingsStore.Save(_settings);
            OnPropertyChanged();
        }
    }

    public bool CloseToTray
    {
        get => _settings.CloseToTray;
        set
        {
            if (_settings.CloseToTray == value)
            {
                return;
            }

            _settings.CloseToTray = value;
            SettingsStore.Save(_settings);
            OnPropertyChanged();
        }
    }

    public bool AutoConnect
    {
        get => _settings.AutoConnect;
        set
        {
            if (_settings.AutoConnect == value)
            {
                return;
            }

            _settings.AutoConnect = value;
            SettingsStore.Save(_settings);
            OnPropertyChanged();

            if (value && _initialized && _client is null)
            {
                _ = ToggleConnectionAsync();
            }
        }
    }

    public bool UseLightTheme
    {
        get => ThemeService.IsLight(_settings.Theme);
        set
        {
            var theme = value ? "light" : "dark";
            if (string.Equals(_settings.Theme, theme, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _settings.Theme = theme;
            SettingsStore.Save(_settings);
            OnPropertyChanged();
            OnPropertyChanged(nameof(ThemeRestartHint));
        }
    }

    public string ThemeRestartHint => "主题在下次启动时生效";

    public bool ShareMusic
    {
        get => _settings.ShareMusic;
        set
        {
            if (_settings.ShareMusic == value)
            {
                return;
            }

            _settings.ShareMusic = value;
            SettingsStore.Save(_settings);
            OnPropertyChanged();
            Refresh();
            _ = _client?.SendNowAsync();
        }
    }

    public string MediaSourceFilter
    {
        get => _settings.MediaSourceFilter;
        set
        {
            var next = value ?? string.Empty;
            if (string.Equals(_settings.MediaSourceFilter, next, StringComparison.Ordinal))
            {
                return;
            }

            _settings.MediaSourceFilter = next;
            SettingsStore.Save(_settings);
            OnPropertyChanged();
            Refresh();
            _ = _client?.SendNowAsync();
        }
    }

    public bool NavRailCollapsed
    {
        get => _settings.NavRailCollapsed;
        set
        {
            if (_settings.NavRailCollapsed == value)
            {
                return;
            }

            _settings.NavRailCollapsed = value;
            SettingsStore.Save(_settings);
            OnPropertyChanged();
        }
    }

    public bool ShareWindowTitles
    {
        get => _settings.ShareWindowTitles;
        set
        {
            if (_settings.ShareWindowTitles == value)
            {
                return;
            }

            _settings.ShareWindowTitles = value;
            SettingsStore.Save(_settings);
            OnPropertyChanged();
            _ = _client?.SendNowAsync();
        }
    }

    public bool NotifyOnClose
    {
        get => _settings.NotifyOnClose;
        set
        {
            if (_settings.NotifyOnClose == value)
            {
                return;
            }

            _settings.NotifyOnClose = value;
            SettingsStore.Save(_settings);
            OnPropertyChanged();
        }
    }

    public string LogPathText => LogService.DirectoryPath;

    public MemberItem? SelectedMember
    {
        get => _selectedMember;
        set => SetProperty(ref _selectedMember, value);
    }

    private string _pokeStatusText = string.Empty;
    private DateTimeOffset _lastPokeAt = DateTimeOffset.MinValue;

    public string PokeStatusText
    {
        get => _pokeStatusText;
        private set => SetProperty(ref _pokeStatusText, value);
    }

    public async Task PokeSelectedAsync()
    {
        var member = SelectedMember;
        if (member is null || !member.CanPoke)
        {
            return;
        }

        if (_client is not { IsConnected: true })
        {
            PokeStatusText = "当前未连接，无法戳一下。";
            return;
        }

        var now = DateTimeOffset.UtcNow;
        if (now - _lastPokeAt < TimeSpan.FromSeconds(2))
        {
            PokeStatusText = "戳得太快了，2 秒后再试。";
            return;
        }

        _lastPokeAt = now;
        PokeStatusText = $"已戳 {member.Name} 一下。";
        await _client.SendPokeAsync(member.Id);
    }

    public bool IsMemberDrawerOpen
    {
        get => _memberDrawerOpen;
        set => SetProperty(ref _memberDrawerOpen, value);
    }

    public void OpenMemberDrawer(MemberItem member)
    {
        SelectedMember = member;
        PokeStatusText = string.Empty;
        IsMemberDrawerOpen = true;
    }

    public void CloseMemberDrawer() => IsMemberDrawerOpen = false;

    public bool StatsEnabled
    {
        get => _settings.StatsEnabled;
        set
        {
            if (_settings.StatsEnabled == value)
            {
                return;
            }

            _settings.StatsEnabled = value;
            SettingsStore.Save(_settings);
            OnPropertyChanged();
            RebuildHistory();
            _ = _client?.SendNowAsync();
        }
    }

    public string TodayFocusText =>
        _settings.StatsEnabled ? FormatDuration(TodaySeconds()) : "统计已关闭";

    public bool CommandEnabled
    {
        get => _settings.CommandEnabled;
        set
        {
            if (_settings.CommandEnabled == value)
            {
                return;
            }

            _settings.CommandEnabled = value;
            SettingsStore.Save(_settings);
            OnPropertyChanged();

            if (value)
            {
                RunCommandNow();
            }
            else
            {
                _commandOutput = string.Empty;
                OnPropertyChanged(nameof(CommandOutputText));
                _ = _client?.SendNowAsync();
            }
        }
    }

    public string CommandExpression
    {
        get => _settings.CommandExpression;
        set
        {
            var expression = value ?? string.Empty;
            if (string.Equals(_settings.CommandExpression, expression, StringComparison.Ordinal))
            {
                return;
            }

            _settings.CommandExpression = expression;
            SettingsStore.Save(_settings);
            OnPropertyChanged();
        }
    }

    public string CommandOutputText =>
        string.IsNullOrWhiteSpace(_commandOutput) ? "（无输出）" : _commandOutput;

    public void RunCommandNow() => _ = RunCommandAsync();

    private async Task RunCommandAsync()
    {
        if (_commandRunning ||
            !_settings.CommandEnabled ||
            string.IsNullOrWhiteSpace(_settings.CommandExpression))
        {
            return;
        }

        _commandRunning = true;
        try
        {
            var output = await CommandRunner.RunAsync(
                _settings.CommandExpression,
                TimeSpan.FromSeconds(3));

            _commandOutput = output ?? string.Empty;
            OnPropertyChanged(nameof(CommandOutputText));
            _ = _client?.SendNowAsync();
        }
        finally
        {
            _commandRunning = false;
        }
    }

    public string WeekFocusText
    {
        get
        {
            if (!_settings.StatsEnabled)
            {
                return "统计已关闭";
            }

            var today = DateTime.Today;
            long total = 0;
            for (var i = 0; i < 7; i++)
            {
                total += _focusHistory.GetValueOrDefault(today.AddDays(-i).ToString("yyyy-MM-dd"));
            }

            return FormatDuration(total);
        }
    }

    public int DailyGoalMinutes
    {
        get => _settings.DailyGoalMinutes;
        set
        {
            var minutes = Math.Clamp(value, 0, 1440);
            if (_settings.DailyGoalMinutes == minutes)
            {
                return;
            }

            _settings.DailyGoalMinutes = minutes;
            SettingsStore.Save(_settings);
            OnPropertyChanged();
            RebuildHistory();
        }
    }

    public double GoalPercent
    {
        get
        {
            if (!_settings.StatsEnabled || _settings.DailyGoalMinutes <= 0)
            {
                return 0;
            }

            return Math.Min(100, TodaySeconds() * 100.0 / (_settings.DailyGoalMinutes * 60.0));
        }
    }

    public string GoalProgressText
    {
        get
        {
            if (!_settings.StatsEnabled)
            {
                return "统计已关闭";
            }

            if (_settings.DailyGoalMinutes <= 0)
            {
                return "未设置每日目标";
            }

            var goal = _settings.DailyGoalMinutes * 60L;
            var today = TodaySeconds();
            if (today >= goal)
            {
                return $"今日目标 {_settings.DailyGoalMinutes} 分 · 已达成";
            }

            var remaining = Math.Max(0, (int)Math.Ceiling((goal - today) / 60.0));
            return $"今日目标 {_settings.DailyGoalMinutes} 分 · 还差 {remaining} 分";
        }
    }

    public string StreakText
    {
        get
        {
            if (!_settings.StatsEnabled || _settings.DailyGoalMinutes <= 0)
            {
                return string.Empty;
            }

            var streak = ComputeStreak();
            return streak > 0 ? $"连续达标 {streak} 天" : "还没有连续达标记录";
        }
    }

    private int ComputeStreak()
    {
        var goalSeconds = _settings.DailyGoalMinutes * 60L;
        var day = DateTime.Today;

        if (_focusHistory.GetValueOrDefault(day.ToString("yyyy-MM-dd")) < goalSeconds)
        {
            day = day.AddDays(-1);
        }

        var count = 0;
        while (count < 3650 &&
               _focusHistory.GetValueOrDefault(day.ToString("yyyy-MM-dd")) >= goalSeconds)
        {
            count++;
            day = day.AddDays(-1);
        }

        return count;
    }

    private long TodaySeconds() =>
        _focusHistory.GetValueOrDefault(DateTime.Today.ToString("yyyy-MM-dd"));

    private void TrackStats(TimeSpan idle, long nowMs)
    {
        if (!_settings.StatsEnabled)
        {
            _lastStatsTickMs = nowMs;
            return;
        }

        if (_lastStatsTickMs > 0 &&
            nowMs - _lastStatsTickMs is > 0 and < 2500 &&
            IsFocusing(idle))
        {
            _statsAccumulatorMs += nowMs - _lastStatsTickMs;
        }

        _lastStatsTickMs = nowMs;

        if (_statsAccumulatorMs >= 1000)
        {
            var seconds = _statsAccumulatorMs / 1000;
            _statsAccumulatorMs %= 1000;
            var key = DateTime.Today.ToString("yyyy-MM-dd");
            _focusHistory[key] = _focusHistory.GetValueOrDefault(key) + seconds;
            RebuildHistoryIfNeeded();
        }

        if (nowMs - _lastStatsPersistMs > 60_000)
        {
            _lastStatsPersistMs = nowMs;
            PersistStats();
        }
    }

    private bool IsFocusing(TimeSpan idle) =>
        _manualMode == "focusing" ||
        (_manualMode == "auto" && idle < TimeSpan.FromMinutes(5));

    private void RebuildHistory()
    {
        _lastHistoryBuild = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
        FocusHistory.Clear();
        var today = DateTime.Today;

        for (var i = 0; i < 7; i++)
        {
            var day = today.AddDays(-i);
            var key = day.ToString("yyyy-MM-dd");
            var label = i switch
            {
                0 => "今天",
                1 => "昨天",
                _ => day.ToString("MM-dd")
            };

            FocusHistory.Add(new FocusDayItem(
                key,
                label,
                FormatDuration(_focusHistory.GetValueOrDefault(key))));
        }

        NotifyStatsChanged();
    }

    private void RebuildHistoryIfNeeded()
    {
        var stamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
        if (string.Equals(stamp, _lastHistoryBuild, StringComparison.Ordinal))
        {
            NotifyStatsChanged();
            return;
        }

        RebuildHistory();
    }

    private void NotifyStatsChanged()
    {
        OnPropertyChanged(nameof(TodayFocusText));
        OnPropertyChanged(nameof(WeekFocusText));
        OnPropertyChanged(nameof(GoalPercent));
        OnPropertyChanged(nameof(GoalProgressText));
        OnPropertyChanged(nameof(StreakText));
    }

    private void LoadStats(string json)
    {
        _focusHistory.Clear();

        if (!string.IsNullOrWhiteSpace(json))
        {
            try
            {
                var map = JsonSerializer.Deserialize<Dictionary<string, long>>(json);
                if (map is not null)
                {
                    foreach (var (key, seconds) in map)
                    {
                        _focusHistory[key] = seconds;
                    }
                }
            }
            catch
            {
                _focusHistory.Clear();
            }
        }

        RebuildHistory();
    }

    private void PersistStats()
    {
        _settings.StatsHistoryJson = JsonSerializer.Serialize(_focusHistory);
        SettingsStore.Save(_settings, notify: false);
    }

    private static string FormatDuration(long seconds)
    {
        if (seconds < 60)
        {
            return $"{seconds} 秒";
        }

        var hours = seconds / 3600;
        var minutes = seconds % 3600 / 60;
        return hours > 0 ? $"{hours} 小时 {minutes} 分" : $"{minutes} 分";
    }

    public bool IsTimerRunning => _timerRunning;

    public string ManualMode => _manualMode;

    public string ManualModeText => _manualMode switch
    {
        "focusing" => "手动：专注",
        "break" => "手动：休息",
        "away" => "手动：离开",
        "invisible" => "手动：隐身",
        _ => "自动（按空闲判断）"
    };

    public bool IsPaused => _paused;

    public string TimerText
    {
        get => _timerText;
        private set => SetProperty(ref _timerText, value);
    }

    /// <summary>开始 / 暂停 / 继续共用一个按钮。</summary>
    public string TimerButtonText => _timerRunning
        ? "暂停"
        : _timerRemainingMs > 0 && _timerRemainingMs < PhaseMinutes * 60_000L
            ? "继续"
            : "开始";

    public ObservableCollection<TodoItem> Todos { get; } = new();

    public ObservableCollection<RoomLogItem> RoomLogs { get; } = new();

    public ObservableCollection<ChatMessageItem> ChatMessages { get; } = new();

    public string ChatInput
    {
        get => _chatInput;
        set => SetProperty(ref _chatInput, value ?? string.Empty);
    }

    public bool IsTodoEmpty => Todos.Count == 0;

    public string? ActiveTodoText
    {
        get
        {
            var active = Todos
                .Where(item => !item.IsCompleted)
                .OrderByDescending(item => item.IsPinned)
                .ThenByDescending(item => item.CreatedAt)
                .FirstOrDefault();

            if (active is null)
            {
                return null;
            }

            return active.HasSubtasks
                ? $"{active.Title} ({active.ProgressText})"
                : active.Title;
        }
    }

    public void AddTodo(string title)
    {
        var text = (title ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            return;
        }

        var item = new TodoItem(
            Guid.NewGuid().ToString("N"),
            text,
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            string.Empty,
            string.Empty,
            false,
            false,
            Array.Empty<SubtaskItem>(),
            OnTodoChanged);

        Todos.Insert(0, item);
        OnTodoChanged();
    }

    public void RemoveTodo(TodoItem item)
    {
        if (Todos.Remove(item))
        {
            OnTodoChanged();
        }
    }

    public void AddSubtask(TodoItem item) => item.AddSubtask();

    public void RemoveSubtask(SubtaskItem subtask)
    {
        foreach (var todo in Todos)
        {
            if (todo.Subtasks.Contains(subtask))
            {
                todo.RemoveSubtask(subtask);
                return;
            }
        }
    }

    private void OnTodoChanged()
    {
        PersistTodos();
        OnPropertyChanged(nameof(ActiveTodoText));
        OnPropertyChanged(nameof(IsTodoEmpty));
        Refresh();
        _todoDebounce.Stop();
        _todoDebounce.Start();
    }

    private void LoadTodos(string json, string legacyText)
    {
        Todos.Clear();
        var migrated = false;

        try
        {
            if (!string.IsNullOrWhiteSpace(json))
            {
                var data = JsonSerializer.Deserialize<List<TodoData>>(json);
                if (data is not null)
                {
                    foreach (var todo in data)
                    {
                        var subtasks = todo.Subtasks.Select(subtask =>
                            new SubtaskItem(subtask.Title, subtask.IsDone));
                        Todos.Add(new TodoItem(
                            string.IsNullOrWhiteSpace(todo.Id) ? Guid.NewGuid().ToString("N") : todo.Id,
                            todo.Title,
                            todo.CreatedAt,
                            todo.StartTime,
                            todo.EndTime,
                            todo.IsDone,
                            todo.IsPinned,
                            subtasks,
                            OnTodoChanged));
                    }
                }
            }

            if (Todos.Count == 0 && !string.IsNullOrWhiteSpace(legacyText))
            {
                Todos.Add(new TodoItem(
                    Guid.NewGuid().ToString("N"),
                    legacyText.Trim(),
                    DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    string.Empty,
                    string.Empty,
                    false,
                    false,
                    Array.Empty<SubtaskItem>(),
                    OnTodoChanged));
                migrated = true;
            }
        }
        catch
        {
            Todos.Clear();
        }

        if (migrated)
        {
            PersistTodos();
        }
    }

    private void PersistTodos()
    {
        var data = Todos
            .Select(todo => new TodoData
            {
                Id = todo.Id,
                Title = todo.Title,
                CreatedAt = todo.CreatedAt,
                StartTime = todo.StartTime,
                EndTime = todo.EndTime,
                IsDone = todo.IsDone,
                IsPinned = todo.IsPinned,
                Subtasks = todo.Subtasks
                    .Select(subtask => new SubtaskData { Title = subtask.Title, IsDone = subtask.IsDone })
                    .ToList()
            })
            .ToList();

        _settings.TodosJson = JsonSerializer.Serialize(data);
        SettingsStore.Save(_settings, notify: false);
    }

    private int PhaseMinutes => _timerPhase == "break" ? _breakMinutes : _focusMinutes;

    public int FocusMinutes
    {
        get => _focusMinutes;
        set
        {
            var minutes = Math.Clamp(value, 1, 600);
            if (_focusMinutes == minutes)
            {
                return;
            }

            _focusMinutes = minutes;
            _settings.TimerFocusMinutes = minutes;
            SettingsStore.Save(_settings);
            OnPropertyChanged();

            if (!_timerRunning && _timerPhase != "break")
            {
                _timerRemainingMs = minutes * 60_000L;
                UpdateTimer(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            }
        }
    }

    public int BreakMinutes
    {
        get => _breakMinutes;
        set
        {
            var minutes = Math.Clamp(value, 1, 600);
            if (_breakMinutes == minutes)
            {
                return;
            }

            _breakMinutes = minutes;
            _settings.TimerBreakMinutes = minutes;
            SettingsStore.Save(_settings);
            OnPropertyChanged();

            if (!_timerRunning && _timerPhase == "break")
            {
                _timerRemainingMs = minutes * 60_000L;
                UpdateTimer(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            }
        }
    }

    public bool TimerCycleEnabled
    {
        get => _timerCycleEnabled;
        set
        {
            if (_timerCycleEnabled == value)
            {
                return;
            }

            _timerCycleEnabled = value;
            _settings.TimerCycleEnabled = value;
            SettingsStore.Save(_settings);
            OnPropertyChanged();
            OnPropertyChanged(nameof(TimerPhaseText));

            if (!value && !_timerRunning)
            {
                _timerPhase = "focus";
                _timerRemainingMs = _focusMinutes * 60_000L;
                OnPropertyChanged(nameof(TimerPhaseText));
                UpdateTimer(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            }
        }
    }

    public string TimerPhaseText => _timerCycleEnabled
        ? (_timerPhase == "break" ? "休息" : "专注")
        : "专注";

    public void StartTimer()
    {
        if (_timerRunning)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var durationMs = PhaseMinutes * 60_000L;
        if (_timerRemainingMs <= 0 || _timerRemainingMs > durationMs)
        {
            _timerRemainingMs = durationMs;
        }

        _timerStartedAt = now - (durationMs - _timerRemainingMs);
        _timerRunning = true;
        OnPropertyChanged(nameof(IsTimerRunning));
            OnPropertyChanged(nameof(TimerButtonText));
        if (_timer is not null)
        {
            _timer.Interval = TimeSpan.FromSeconds(1);
        }

        UpdateTimer(now);
        _ = _client?.SendNowAsync();
    }

    public void PauseTimer()
    {
        if (!_timerRunning)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        _timerRemainingMs = Math.Max(0, _timerStartedAt + PhaseMinutes * 60_000L - now);
        _timerRunning = false;
        OnPropertyChanged(nameof(IsTimerRunning));
            OnPropertyChanged(nameof(TimerButtonText));
        if (_timer is not null)
        {
            _timer.Interval = TimeSpan.FromSeconds(2);
        }

        UpdateTimer(now);
        _ = _client?.SendNowAsync();
    }

    public void ResetTimer()
    {
        _timerRunning = false;
        OnPropertyChanged(nameof(IsTimerRunning));
            OnPropertyChanged(nameof(TimerButtonText));
        _timerPhase = "focus";
        OnPropertyChanged(nameof(TimerPhaseText));
        _timerRemainingMs = _focusMinutes * 60_000L;
        if (_timer is not null)
        {
            _timer.Interval = TimeSpan.FromSeconds(2);
        }

        UpdateTimer(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        _ = _client?.SendNowAsync();
    }

    public void SetTimerMinutes(int minutes) => FocusMinutes = minutes;

    /// <summary>当前阶段时长（分钟）。</summary>
    public int TimerDurationMinutes => PhaseMinutes;
    public void ToggleTimer()
    {
        if (_timerRunning)
        {
            PauseTimer();
        }
        else
        {
            StartTimer();
        }
    }

    public void MoveLayoutBlock(StatusBlockItem item, int delta)
    {
        var index = LayoutBlocks.IndexOf(item);
        var target = index + delta;
        if (index < 0 || target < 0 || target >= LayoutBlocks.Count)
        {
            return;
        }

        LayoutBlocks.Move(index, target);
        OnLayoutChanged();
    }

    public void MoveLayoutBlockBefore(StatusBlockItem source, StatusBlockItem target)
    {
        if (ReferenceEquals(source, target))
        {
            return;
        }

        var from = LayoutBlocks.IndexOf(source);
        var to = LayoutBlocks.IndexOf(target);
        if (from < 0 || to < 0)
        {
            return;
        }

        LayoutBlocks.Move(from, to > from ? to - 1 : to);
        OnLayoutChanged();
    }

    public void MoveLayoutBlockToIndex(StatusBlockItem item, int index)
    {
        var from = LayoutBlocks.IndexOf(item);
        if (from < 0)
        {
            return;
        }

        var target = Math.Clamp(index, 0, LayoutBlocks.Count);
        if (target > from)
        {
            target--;
        }

        if (target == from)
        {
            return;
        }

        LayoutBlocks.Move(from, target);
        OnLayoutChanged();
    }

    public void AddLayoutBlock(string type)
    {
        if (LayoutBlocks.Any(block => string.Equals(block.Type, type, StringComparison.Ordinal)))
        {
            return;
        }

        var rule = new StatusBlockRule
        {
            Type = type,
            Enabled = true,
            Template = DefaultTemplate(type)
        };

        AddLayoutBlock(rule);
        OnLayoutChanged();
    }

    public string LayoutNotice { get; private set; } = string.Empty;

    public void AddCustomBlock()
    {
        var count = LayoutBlocks.Count(block => block.Type == "text");
        if (count >= 5)
        {
            LayoutNotice = "最多只能添加 5 个自定义块";
            OnPropertyChanged(nameof(LayoutNotice));
            return;
        }

        LayoutNotice = string.Empty;
        OnPropertyChanged(nameof(LayoutNotice));

        AddLayoutBlock(new StatusBlockRule
        {
            Type = "text",
            Enabled = true,
            Template = "专注中"
        });

        OnLayoutChanged();
    }

    public void RemoveLayoutBlock(StatusBlockItem item)
    {
        if (!LayoutBlocks.Remove(item))
        {
            return;
        }

        LayoutNotice = string.Empty;
        OnPropertyChanged(nameof(LayoutNotice));
        OnLayoutChanged();
    }

    private static string DefaultTemplate(string type) => type switch
    {
        "app" => StatusComposer.AppTemplate,
        "media" => StatusComposer.MediaTemplate,
        "timer" => StatusComposer.TimerTemplate,
        "todo" => StatusComposer.TodoTemplate,
        _ => string.Empty
    };

    public void ResetLayout()
    {
        _settings.StatusLayoutJson = string.Empty;
        LayoutBlocks.Clear();
        foreach (var rule in StatusLayouts.Default())
        {
            AddLayoutBlock(rule);
        }

        OnLayoutChanged();
    }

    private void LoadLayout(string json)
    {
        LayoutBlocks.Clear();

        List<StatusBlockRule> rules;
        try
        {
            rules = string.IsNullOrWhiteSpace(json)
                ? StatusLayouts.Default()
                : JsonSerializer.Deserialize<List<StatusBlockRule>>(json) ?? StatusLayouts.Default();
        }
        catch
        {
            rules = StatusLayouts.Default();
        }

        if (rules.Count == 0)
        {
            rules = StatusLayouts.Default();
        }

        rules = rules.Where(rule => rule.Type is not ("command" or "uptime")).ToList();
        if (rules.Count == 0)
        {
            rules = StatusLayouts.Default();
        }

        foreach (var rule in rules)
        {
            AddLayoutBlock(rule);
        }
    }

    private void AddLayoutBlock(StatusBlockRule rule) =>
        LayoutBlocks.Add(new StatusBlockItem(
            rule.Type,
            rule.Type == "text"
                ? $"自定义 {LayoutBlocks.Count(block => block.Type == "text") + 1}"
                : StatusLayouts.Label(rule.Type),
            rule.Template,
            rule.Enabled,
            OnLayoutChanged));

    private void OnLayoutChanged()
    {
        Refresh();
        _layoutDebounce?.Stop();
        _layoutDebounce?.Start();
    }

    private void PersistLayout()
    {
        var rules = LayoutBlocks
            .Select(block => new StatusBlockRule
            {
                Type = block.Type,
                Enabled = block.Enabled,
                Template = block.Template
            })
            .ToList();

        _settings.StatusLayoutJson = JsonSerializer.Serialize(rules);
        SettingsStore.Save(_settings, notify: false);
    }

    private IReadOnlyList<StatusBlockRule> CurrentLayout(string appId, string category)
    {
        var rule = FindAppRule(appId);
        var categoryRule = FindAppCategory(category);
        var overrideTemplate = !string.IsNullOrWhiteSpace(rule?.Template)
            ? rule!.Template
            : categoryRule?.Template;
        var ignored = rule?.Ignored == true;

        return LayoutBlocks
            .Where(block => block.Type is not ("command" or "uptime"))
            .Select(block => new StatusBlockRule
            {
                Type = block.Type,
                Enabled = block.Enabled && !(block.Type == "app" && ignored),
                Template = block.Type == "app" && !string.IsNullOrWhiteSpace(overrideTemplate)
                    ? overrideTemplate
                    : block.Type == "text"
                        ? _scriptRunner.Expand(block.Template)
                        : block.Template
            })
            .ToList();
    }

    public AppRuleItem? SelectedAppRule
    {
        get => _selectedAppRule;
        set
        {
            if (ReferenceEquals(_selectedAppRule, value))
            {
                return;
            }

            if (_selectedAppRule is not null)
            {
                _selectedAppRule.IsSelected = false;
            }

            _selectedAppRule = value;

            if (_selectedAppRule is not null)
            {
                _selectedAppRule.IsSelected = true;
            }

            OnPropertyChanged();
        }
    }

    public bool IsAppEditorOpen
    {
        get => _isAppEditorOpen;
        set => SetProperty(ref _isAppEditorOpen, value);
    }

    public AppRuleDraft? AppDraft
    {
        get => _appDraft;
        private set => SetProperty(ref _appDraft, value);
    }

    public bool IsAppRuleDirty
    {
        get => _isAppRuleDirty;
        private set => SetProperty(ref _isAppRuleDirty, value);
    }

    public void OpenAppEditor(AppRuleItem item)
    {
        SelectedAppRule = item;

        if (string.IsNullOrEmpty(item.ExecutablePath) && !string.IsNullOrWhiteSpace(item.ProcessName))
        {
            var path = IconCache.GetExecutablePath(0, item.ProcessName);
            if (!string.IsNullOrWhiteSpace(path))
            {
                item.UpdateExecutablePath(path);
                _layoutDebounce?.Stop();
                _layoutDebounce?.Start();
            }
        }

        if (_appDraft is not null)
        {
            _appDraft.PropertyChanged -= Draft_PropertyChanged;
        }

        AppDraft = new AppRuleDraft(item);
        AppDraft.PropertyChanged += Draft_PropertyChanged;
        IsAppRuleDirty = false;
        IsAppEditorOpen = true;
    }

    public void CloseAppEditor() => CancelAppRuleDraft();

    public void SaveAppRuleDraft()
    {
        if (SelectedAppRule is not { } item || AppDraft is not { } draft)
        {
            EndAppDraft();
            return;
        }

        item.DisplayName = draft.DisplayName;
        item.ExecutablePath = draft.ExecutablePath;
        item.Template = draft.Template;
        item.CategoryOverride = draft.CategoryOverride;
        item.Ignored = draft.Ignored;
        item.HideTitle = draft.HideTitle;

        if (draft.ResetIcon)
        {
            item.CustomIconPath = string.Empty;
            item.Icon = IconCache.Load(item.AppId);
        }
        else if (!string.IsNullOrWhiteSpace(draft.PendingIconPath))
        {
            var path = IconCache.IsManagedCustomIcon(draft.PendingIconPath)
                ? draft.PendingIconPath
                : IconCache.SaveCustomIcon(item.AppId, draft.PendingIconPath);
            if (!string.IsNullOrEmpty(path))
            {
                item.CustomIconPath = path;
                item.Icon = IconCache.LoadFile(path);
            }
        }

        NotifyToast("应用规则已保存", "应用名称、分类与状态模板已更新。", ToastKind.Success, 3000);
        EndAppDraft();
    }

    public void CancelAppRuleDraft() => EndAppDraft();

    public void ApplyDraftIcon(string sourcePath)
    {
        if (AppDraft is not { } draft)
        {
            return;
        }

        var iconPath = IconCache.IsImageFile(sourcePath)
            ? sourcePath
            : IconCache.ExtractCustomIcon(draft.AppId, sourcePath);

        if (string.IsNullOrEmpty(iconPath))
        {
            ShowNotice("无法从该文件读取图标，请换一个 .exe 或图片文件。");
            NotifyToast("图标读取失败", "请换一个 .exe 或图片文件。", ToastKind.Error, 7000);
            return;
        }

        var image = IconCache.LoadFile(iconPath);
        if (image is null)
        {
            ShowNotice("无法加载图标文件。");
            NotifyToast("图标读取失败", "无法加载该图标文件。", ToastKind.Error, 7000);
            return;
        }

        NotifyToast("图标已更新", "应用图标已载入，保存规则后生效。", ToastKind.Success, 3000);

        draft.PendingIconPath = iconPath;
        draft.ResetIcon = false;
        draft.Icon = image;
        IsAppRuleDirty = true;
    }

    public void ResetDraftIcon()
    {
        if (AppDraft is not { } draft)
        {
            return;
        }

        draft.PendingIconPath = string.Empty;
        draft.ResetIcon = true;
        draft.Icon = IconCache.Load(draft.AppId);
        IsAppRuleDirty = true;
    }

    private void Draft_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) =>
        IsAppRuleDirty = true;

    private void EndAppDraft()
    {
        if (_appDraft is not null)
        {
            _appDraft.PropertyChanged -= Draft_PropertyChanged;
        }

        IsAppRuleDirty = false;
        IsAppEditorOpen = false;
    }

    private AppRuleItem? FindAppRule(string appId) =>
        _appRuleMap.GetValueOrDefault(appId);

    private AppRuleItem? EnsureAppRule(AppMatch match, ForegroundInfo? foreground)
    {
        if (string.IsNullOrWhiteSpace(match.Id) || HiddenAppIds.Contains(match.Id))
        {
            return null;
        }

        if (_appRuleMap.TryGetValue(match.Id, out var existing))
        {
            if (foreground is not null && string.IsNullOrEmpty(existing.ExecutablePath))
            {
                existing.UpdateExecutablePath(
                    IconCache.GetExecutablePath(foreground.ProcessId, foreground.ProcessName) ?? string.Empty);
                _layoutDebounce?.Stop();
                _layoutDebounce?.Start();
            }

            return existing;
        }

        var display = string.IsNullOrWhiteSpace(match.Name) || match.Name == "unknown"
            ? _catalog.DisplayName(match.Id)
            : match.Name;
        if (string.IsNullOrWhiteSpace(display))
        {
            display = match.Id;
        }

        var executablePath = foreground is null
            ? string.Empty
            : IconCache.GetExecutablePath(foreground.ProcessId, foreground.ProcessName) ?? string.Empty;

        var item = new AppRuleItem(
            match.Id,
            display,
            foreground?.ProcessName ?? string.Empty,
            match.Category,
            string.Empty,
            string.Empty,
            false,
            false,
            OnAppRuleChanged,
            executablePath,
            string.Empty,
            DateTimeOffset.UtcNow)
        {
            Icon = IconCache.Load(match.Id)
        };

        if (item.Icon is null && foreground is not null)
        {
            item.Icon = IconCache.GetOrCreate(match.Id, foreground.ProcessId, foreground.ProcessName);
        }

        AppRules.Add(item);
        _appRuleMap[match.Id] = item;
        UpdateAppCategoryDisplayNames();
        OnPropertyChanged(nameof(IsAppListEmpty));
        ApplyAppSort();

        if (SelectedAppRule is null)
        {
            SelectedAppRule = item;
        }

        _layoutDebounce?.Stop();
        _layoutDebounce?.Start();
        return item;
    }

    private void OnAppRuleChanged()
    {
        UpdateAppCategoryDisplayNames();
        ApplyAppSort();
        Refresh();
        _layoutDebounce?.Stop();
        _layoutDebounce?.Start();
    }

    private void LoadAppRules(string json, string legacyTemplates)
    {
        AppRules.Clear();
        _appRuleMap.Clear();

        var items = new List<AppRuleItem>();

        try
        {
            if (!string.IsNullOrWhiteSpace(json))
            {
                var data = JsonSerializer.Deserialize<List<AppRuleData>>(json);
                if (data is not null)
                {
                    foreach (var rule in data)
                    {
                        AddLoadedAppRule(rule, items);
                    }
                }
            }
            else if (!string.IsNullOrWhiteSpace(legacyTemplates))
            {
                var map = JsonSerializer.Deserialize<Dictionary<string, string>>(legacyTemplates);
                if (map is not null)
                {
                    foreach (var (id, template) in map)
                    {
                        AddLoadedAppRule(
                            new AppRuleData { AppId = id, Template = template },
                            items);
                    }
                }
            }
        }
        catch
        {
            AppRules.Clear();
            _appRuleMap.Clear();
            items.Clear();
        }

        OnPropertyChanged(nameof(IsAppListEmpty));
        SelectedAppRule = AppRules.FirstOrDefault();
    }

    private void AddLoadedAppRule(AppRuleData data, List<AppRuleItem> items)
    {
        var id = data.AppId.Trim().ToLowerInvariant();
        if (id.Length == 0 || HiddenAppIds.Contains(id) || _appRuleMap.ContainsKey(id))
        {
            return;
        }

        var display = string.IsNullOrWhiteSpace(data.DisplayName) ? _catalog.DisplayName(id) : data.DisplayName;
        if (string.IsNullOrWhiteSpace(display))
        {
            display = id;
        }

        var item = new AppRuleItem(
            id,
            display,
            data.ProcessName,
            string.IsNullOrWhiteSpace(data.DefaultCategory) ? "other" : data.DefaultCategory,
            data.Template,
            data.CategoryOverride,
            data.Ignored,
            data.HideTitle,
            OnAppRuleChanged,
            data.ExecutablePath,
            data.CustomIconPath,
            data.LastSeenAt)
        {
            Icon = !string.IsNullOrEmpty(data.CustomIconPath)
                ? IconCache.LoadFile(data.CustomIconPath) ?? IconCache.Load(id)
                : IconCache.Load(id)
        };

        _appRuleMap[id] = item;
        AppRules.Add(item);
        items.Add(item);
    }

    private void LoadAppCategories(string json)
    {
        AppCategories.Clear();
        _appCategoryMap.Clear();

        try
        {
            if (!string.IsNullOrWhiteSpace(json))
            {
                var data = JsonSerializer.Deserialize<List<AppCategoryData>>(json);
                if (data is not null)
                {
                    foreach (var category in data)
                    {
                        AddLoadedCategory(category.Id, category.Name, category.Template, category.IsDefault);
                    }
                }
            }
        }
        catch
        {
            AppCategories.Clear();
            _appCategoryMap.Clear();
        }

        if (AppCategories.Count == 0)
        {
            foreach (var (id, name) in DefaultCategories())
            {
                AddLoadedCategory(id, name, string.Empty, true);
            }
        }

        UpdateAppCategoryDisplayNames();
    }

    private void AddLoadedCategory(string id, string name, string template, bool isDefault)
    {
        var normalized = (id ?? string.Empty).Trim().ToLowerInvariant();
        if (normalized.Length == 0 || _appCategoryMap.ContainsKey(normalized))
        {
            return;
        }

        var item = new AppCategoryItem(
            normalized,
            string.IsNullOrWhiteSpace(name) ? normalized : name,
            template,
            isDefault,
            OnAppCategoryChanged);
        _appCategoryMap[normalized] = item;
        AppCategories.Add(item);
    }

    private static IEnumerable<(string Id, string Name)> DefaultCategories() => new[]
    {
        ("coding", "编程"),
        ("writing", "写作"),
        ("reading", "阅读"),
        ("meeting", "会议"),
        ("media", "媒体"),
        ("game", "游戏"),
        ("browsing", "浏览"),
        ("other", "其他")
    };

    private AppCategoryItem? FindAppCategory(string? id) =>
        string.IsNullOrEmpty(id) ? null : _appCategoryMap.GetValueOrDefault(id);

    private string CategoryName(string id) =>
        FindAppCategory(id)?.Name ?? StatusLayouts.Label(id);

    public void AddAppCategory(string name)
    {
        var label = (name ?? string.Empty).Trim();
        if (label.Length == 0 || label.Length > 16)
        {
            return;
        }

        var id = "cat-" + Guid.NewGuid().ToString("n")[..8];
        AddLoadedCategory(id, label, string.Empty, false);
        PersistAppCategories();
        UpdateAppCategoryDisplayNames();
        OnLayoutChanged();
    }

    public void RemoveAppCategory(AppCategoryItem item)
    {
        if (!AppCategories.Remove(item))
        {
            return;
        }

        _appCategoryMap.Remove(item.Id);
        foreach (var rule in AppRules.Where(rule =>
                     string.Equals(rule.CategoryOverride, item.Id, StringComparison.Ordinal)))
        {
            rule.CategoryOverride = string.Empty;
        }

        PersistAppCategories();
        PersistAppRules();
        UpdateAppCategoryDisplayNames();
        OnLayoutChanged();
    }

    private void OnAppCategoryChanged()
    {
        PersistAppCategories();
        UpdateAppCategoryDisplayNames();
        if (HasAppSearch)
        {
            AppRulesView.Refresh();
            OnPropertyChanged(nameof(IsAppFilterEmpty));
        }

        Refresh();
        _layoutDebounce?.Stop();
        _layoutDebounce?.Start();
    }

    private void UpdateAppCategoryDisplayNames()
    {
        foreach (var rule in AppRules)
        {
            var category = FindAppCategory(rule.EffectiveCategory);
            rule.UpdateCategoryDisplayName(
                category?.Name ?? StatusLayouts.Label(rule.EffectiveCategory));
        }
    }

    public void MoveAppCategory(AppCategoryItem source, AppCategoryItem target)
    {
        var from = AppCategories.IndexOf(source);
        var to = AppCategories.IndexOf(target);
        if (from < 0 || to < 0 || from == to)
        {
            return;
        }

        AppCategories.Move(from, to);
        PersistAppCategories();
    }

    public void MoveAppCategoryToIndex(AppCategoryItem source, int index)
    {
        var from = AppCategories.IndexOf(source);
        if (from < 0)
        {
            return;
        }

        var to = Math.Clamp(index, 0, AppCategories.Count);
        if (to > from)
        {
            to--;
        }

        if (to == from)
        {
            return;
        }

        AppCategories.Move(from, to);
        PersistAppCategories();
    }

    private void PersistAppCategories()
    {
        var data = AppCategories
            .Select(category => new AppCategoryData
            {
                Id = category.Id,
                Name = category.Name,
                Template = category.Template,
                IsDefault = category.IsDefault
            })
            .ToList();

        _settings.AppCategoriesJson = JsonSerializer.Serialize(data);
        SettingsStore.Save(_settings, notify: false);
    }

    private void PersistAppRules()
    {
        var data = AppRules
            .Select(rule => new AppRuleData
            {
                AppId = rule.AppId,
                DisplayName = rule.DisplayName,
                ProcessName = rule.ProcessName,
                DefaultCategory = rule.DefaultCategory,
                Template = rule.Template,
                CategoryOverride = rule.CategoryOverride,
                Ignored = rule.Ignored,
                HideTitle = rule.HideTitle,
                ExecutablePath = rule.ExecutablePath,
                CustomIconPath = rule.CustomIconPath,
                LastSeenAt = rule.LastSeenAt
            })
            .ToList();

        _settings.AppRulesJson = JsonSerializer.Serialize(data);
        SettingsStore.Save(_settings, notify: false);
    }

    public void AddTitleWhitelist(string appId)
    {
        var id = (appId ?? string.Empty).Trim().ToLowerInvariant();
        if (id.Length == 0 || TitleWhitelist.Any(item => string.Equals(item.AppId, id, StringComparison.Ordinal)))
        {
            return;
        }

        var display = _catalog.DisplayName(id);
        TitleWhitelist.Add(new TitleWhitelistItem(id, string.IsNullOrWhiteSpace(display) ? id : display));
        PersistTitleWhitelist();
        _ = _client?.SendNowAsync();
    }

    public void RemoveTitleWhitelist(TitleWhitelistItem item)
    {
        if (TitleWhitelist.Remove(item))
        {
            PersistTitleWhitelist();
            _ = _client?.SendNowAsync();
        }
    }

    private void LoadTitleWhitelist(string json)
    {
        TitleWhitelist.Clear();

        if (string.IsNullOrWhiteSpace(json))
        {
            return;
        }

        try
        {
            var ids = JsonSerializer.Deserialize<List<string>>(json);
            if (ids is null)
            {
                return;
            }

            foreach (var id in ids.Where(id => !string.IsNullOrWhiteSpace(id)))
            {
                var display = _catalog.DisplayName(id);
                TitleWhitelist.Add(new TitleWhitelistItem(
                    id,
                    string.IsNullOrWhiteSpace(display) ? id : display));
            }
        }
        catch
        {
            TitleWhitelist.Clear();
        }
    }

    private void PersistTitleWhitelist()
    {
        _settings.TitleWhitelistJson = JsonSerializer.Serialize(
            TitleWhitelist.Select(item => item.AppId).ToList());
        SettingsStore.Save(_settings, notify: false);
    }

    private void UpdateTimer(long nowMs)
    {
        if (_timerRunning)
        {
            var remaining = _timerStartedAt + PhaseMinutes * 60_000L - nowMs;
            if (remaining <= 0)
            {
                var finishedPhase = _timerPhase;
                NotifyToast(
                    finishedPhase == "break" ? "休息结束" : "专注结束",
                    finishedPhase == "break" ? "休息时间到，准备开始下一轮专注。" : "专注时间到，起身休息一下吧。",
                    ToastKind.Success,
                    7000);

                _timerRunning = false;
                _timerRemainingMs = 0;

                if (_timerCycleEnabled)
                {
                    _timerPhase = _timerPhase == "break" ? "focus" : "break";
                    OnPropertyChanged(nameof(TimerPhaseText));
                    OnPropertyChanged(nameof(TimerDurationMinutes));
                    _timerRemainingMs = PhaseMinutes * 60_000L;
                    StartTimer();
                    return;
                }

                if (_timerPhase == "break")
                {
                    _timerPhase = "focus";
                    OnPropertyChanged(nameof(TimerPhaseText));
                    OnPropertyChanged(nameof(TimerDurationMinutes));
                }

                OnPropertyChanged(nameof(IsTimerRunning));
            OnPropertyChanged(nameof(TimerButtonText));
                _timerRemainingMs = 0;
                if (_timer is not null)
                {
                    _timer.Interval = TimeSpan.FromSeconds(2);
                }

                TimerText = "时间到";
                _ = _client?.SendNowAsync();
            }
            else
            {
                _timerRemainingMs = remaining;
                TimerText = FormatRemaining(remaining);
            }

            return;
        }

        if (_timerRemainingMs > 0 && _timerRemainingMs < PhaseMinutes * 60_000L)
        {
            TimerText = $"已暂停 {FormatRemaining(_timerRemainingMs)}";
        }
        else
        {
            TimerText = FormatRemaining(PhaseMinutes * 60_000L);
        }
    }
    private static string FormatRemaining(long milliseconds)
    {
        var totalSeconds = Math.Max(0, (int)Math.Ceiling(milliseconds / 1000.0));
        return $"{totalSeconds / 60:00}:{totalSeconds % 60:00}";
    }

    public void SetManualMode(string mode)
    {
        if (string.Equals(_manualMode, mode, StringComparison.Ordinal))
        {
            return;
        }

        _manualMode = mode;
        OnPropertyChanged(nameof(ManualMode));
        OnPropertyChanged(nameof(ManualModeText));

        if (_client is not null)
        {
            _ = _client.SendNowAsync();
        }

        Refresh();
    }

    public void SetPaused(bool paused)
    {
        if (_paused == paused)
        {
            return;
        }

        _paused = paused;

        if (_client is not null)
        {
            _client.Paused = paused;
            if (!paused)
            {
                _ = _client.SendNowAsync();
            }
        }

        OnPropertyChanged(nameof(IsPaused));
        Refresh();
    }

    public ImageSource? MyAvatarImage
    {
        get => _myAvatarImage;
        private set
        {
            if (SetProperty(ref _myAvatarImage, value))
            {
                OnPropertyChanged(nameof(HasMyAvatar));
            }
        }
    }

    public bool HasMyAvatar => _myAvatarImage is not null;

    public string MyAvatarInitial => string.IsNullOrWhiteSpace(_settings.Name) ? "?" : _settings.Name[..1];

    public async Task ChangeAvatarAsync(string path) =>
        await ApplyAvatarAsync(AvatarStore.CreateFromFile(path));

    public async Task ChangeAvatarAsync(BitmapSource image) =>
        await ApplyAvatarAsync(AvatarStore.CreateFromImage(image));

    private async Task ApplyAvatarAsync(AvatarPayload? avatar)
    {
        if (avatar is null)
        {
            ShowNotice("无法读取或压缩该图片，请换一张不超过 512×512 的图片。");
            NotifyToast("头像保存失败", "无法读取或压缩该图片，请换一张。", ToastKind.Error, 7000);
            return;
        }

        if (!AvatarStore.Save(avatar))
        {
            ShowNotice("头像保存失败，请检查本地缓存目录。");
            NotifyToast("头像保存失败", "无法写入头像缓存目录。", ToastKind.Error, 7000);
            return;
        }

        NotifyToast("头像已更新", "新头像已保存并同步。", ToastKind.Success, 3000);

        _avatar = avatar;
        MyAvatarImage = AvatarStore.ToImage(avatar, 128);
        OnPropertyChanged(nameof(MyAvatarInitial));

        if (_client is { IsConnected: true } client)
        {
            await client.SendAvatarAsync(avatar);
        }
    }

    public string MyStatusText
    {
        get => _myStatusText;
        private set => SetProperty(ref _myStatusText, value);
    }

    public string OverlayHeader
    {
        get => _overlayHeader;
        private set => SetProperty(ref _overlayHeader, value);
    }

    public string MyAppText
    {
        get => _myAppText;
        private set => SetProperty(ref _myAppText, value);
    }

    public string MyMediaText
    {
        get => _myMediaText;
        private set => SetProperty(ref _myMediaText, value);
    }

    public string MyIdleText
    {
        get => _myIdleText;
        private set => SetProperty(ref _myIdleText, value);
    }

    public string LastSyncText
    {
        get => _lastSyncText;
        private set => SetProperty(ref _lastSyncText, value);
    }

    public string TrayText
    {
        get => _trayText;
        private set => SetProperty(ref _trayText, value);
    }

    public string NoticeText
    {
        get => _noticeText;
        private set => SetProperty(ref _noticeText, value);
    }

    public bool TrayAvailable
    {
        get => _trayAvailable;
        set => SetProperty(ref _trayAvailable, value);
    }

    public bool OverlayEnabled
    {
        get => _settings.OverlayEnabled;
        set
        {
            if (_settings.OverlayEnabled == value)
            {
                return;
            }

            _settings.OverlayEnabled = value;
            SettingsStore.Save(_settings);
            OnPropertyChanged();
            OverlayVisibilityChanged?.Invoke();
        }
    }

    public bool OverlayClickThrough
    {
        get => _settings.OverlayClickThrough;
        set
        {
            if (_settings.OverlayClickThrough == value)
            {
                return;
            }

            _settings.OverlayClickThrough = value;
            SettingsStore.Save(_settings);
            OnPropertyChanged();
            OverlayClickThroughChanged?.Invoke();
        }
    }

    public static IReadOnlyList<OverlayPresetOption> OverlayPresetOptions { get; } = new[]
    {
        new OverlayPresetOption("minimal", "极简单行"),
        new OverlayPresetOption("compact", "紧凑双行"),
        new OverlayPresetOption("card", "卡片"),
        new OverlayPresetOption("subtitle", "字幕条"),
        new OverlayPresetOption("custom", "自定义")
    };

    public IReadOnlyList<OverlayPresetOption> Presets => OverlayPresetOptions;

    public string OverlayPreset
    {
        get => _settings.OverlayPreset;
        set
        {
            if (string.Equals(_settings.OverlayPreset, value, StringComparison.Ordinal))
            {
                return;
            }

            _settings.OverlayPreset = value;
            SettingsStore.Save(_settings);
            OnPropertyChanged();
            OverlayThemeChanged?.Invoke();
        }
    }

    public OverlayTheme GetEffectiveTheme() =>
        string.Equals(OverlayPreset, "custom", StringComparison.Ordinal)
            ? _customTheme
            : OverlayThemes.GetPreset(OverlayPreset);

    public void SaveCustomTheme(OverlayTheme theme)
    {
        _customTheme = theme;
        _settings.OverlayThemeJson = JsonSerializer.Serialize(theme);
        _settings.OverlayPreset = "custom";
        SettingsStore.Save(_settings);
        OnPropertyChanged(nameof(OverlayPreset));
        OverlayThemeChanged?.Invoke();
    }

    public void PreviewCustomTheme(OverlayTheme theme)
    {
        _customTheme = theme;
        _settings.OverlayPreset = "custom";
        OnPropertyChanged(nameof(OverlayPreset));
        OverlayThemeChanged?.Invoke();
    }

    public void CopyCurrentToCustom() => SaveCustomTheme(GetEffectiveTheme());

    private static OverlayTheme LoadCustomTheme(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return OverlayThemes.DefaultCustom;
        }

        try
        {
            return JsonSerializer.Deserialize<OverlayTheme>(json) ?? OverlayThemes.DefaultCustom;
        }
        catch
        {
            return OverlayThemes.DefaultCustom;
        }
    }

    public string VersionText => typeof(MainViewModel).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";

    public bool IsCheckingUpdate => _isCheckingUpdate;

    public bool IsUpdateAvailable => _isUpdateAvailable;

    public string UpdateStatusText => _updateStatusText;

    public async Task CheckForUpdatesAsync()
    {
        if (_isCheckingUpdate)
        {
            return;
        }

        _isCheckingUpdate = true;
        _isUpdateAvailable = false;
        _updateDownloadUrl = string.Empty;
        _updateStatusText = "正在检查更新…";
        NotifyUpdateState();

        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(12) };
            try
            {
                using var manifestResponse = await http.GetAsync(
                    "https://raw.githubusercontent.com/Miyu3710/9mRoom/main/update-windows.json");
                if (manifestResponse.IsSuccessStatusCode)
                {
                    using var manifest = JsonDocument.Parse(
                        await manifestResponse.Content.ReadAsStringAsync());
                    var manifestRoot = manifest.RootElement;
                    var manifestVersion = manifestRoot.TryGetProperty("version", out var versionElement)
                        ? versionElement.GetString()
                        : null;
                    var manifestTag = manifestRoot.TryGetProperty("tag", out var tagElement)
                        ? tagElement.GetString()
                        : null;
                    var manifestAsset = manifestRoot.TryGetProperty("windows", out var windowsElement) &&
                                        windowsElement.TryGetProperty("asset", out var assetElement)
                        ? assetElement.GetString()
                        : null;

                    if (TryParseVersion(manifestVersion ?? string.Empty, out var manifestLatest) &&
                        TryParseVersion(VersionText, out var manifestCurrent) &&
                        !string.IsNullOrWhiteSpace(manifestTag) &&
                        !string.IsNullOrWhiteSpace(manifestAsset))
                    {
                        _updateDownloadUrl =
                            $"https://github.com/Miyu3710/9mRoom/releases/download/{manifestTag}/{manifestAsset}";
                        if (manifestLatest > manifestCurrent)
                        {
                            _isUpdateAvailable = true;
                            _updateStatusText = $"发现新版本 9mRoom {manifestLatest}";
                        }
                        else
                        {
                            _updateStatusText = $"已是最新版本（{VersionText}）";
                        }

                        return;
                    }
                }
            }
            catch
            {
                // 稳定清单不可用时回退到 Releases Atom
            }

            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                "https://github.com/Miyu3710/9mRoom/releases.atom");
            request.Headers.UserAgent.ParseAdd("9mRoom/" + VersionText);

            using var response = await http.SendAsync(request);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _updateStatusText = "GitHub 上还没有发布版本";
                return;
            }

            if (!response.IsSuccessStatusCode)
            {
                _updateStatusText = $"检查失败（HTTP {(int)response.StatusCode}）";
                return;
            }

            XNamespace ns = "http://www.w3.org/2005/Atom";
            var document = XDocument.Parse(await response.Content.ReadAsStringAsync());
            var entries = document.Root?.Elements(ns + "entry").ToList() ?? new List<XElement>();
            var entry = entries.FirstOrDefault(candidate =>
            {
                var link = candidate.Elements(ns + "link")
                    .FirstOrDefault(item => string.Equals((string?)item.Attribute("rel"), "alternate", StringComparison.Ordinal))
                    ?.Attribute("href")?.Value ?? string.Empty;
                var candidateTag = link.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? string.Empty;
                return candidateTag.StartsWith("windows-v", StringComparison.OrdinalIgnoreCase);
            }) ?? entries.FirstOrDefault(candidate =>
                (candidate.Element(ns + "title")?.Value ?? string.Empty)
                .Contains("Windows", StringComparison.OrdinalIgnoreCase));
            if (entry is null)
            {
                _updateStatusText = "GitHub 上还没有 Windows 发布版本";
                return;
            }

            var title = entry.Element(ns + "title")?.Value.Trim() ?? string.Empty;
            var releaseUrl = entry.Elements(ns + "link")
                .FirstOrDefault(link => string.Equals((string?)link.Attribute("rel"), "alternate", StringComparison.Ordinal))
                ?.Attribute("href")?.Value ?? string.Empty;
            var releaseTag = releaseUrl.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? string.Empty;
            var tag = string.IsNullOrWhiteSpace(releaseTag) ? title : releaseTag;

            _updateDownloadUrl = releaseUrl;
            try
            {
                var manifestUrl =
                    $"https://github.com/Miyu3710/9mRoom/releases/download/{releaseTag}/update.json";
                using var manifest = JsonDocument.Parse(await http.GetStringAsync(manifestUrl));
                if (manifest.RootElement.TryGetProperty("windows", out var windows) &&
                    windows.TryGetProperty("asset", out var assetElement))
                {
                    var assetName = assetElement.GetString();
                    if (!string.IsNullOrWhiteSpace(assetName))
                    {
                        _updateDownloadUrl =
                            $"https://github.com/Miyu3710/9mRoom/releases/download/{releaseTag}/{assetName}";
                    }
                }
            }
            catch
            {
                // 没有清单时回退到 Release 页面
            }

            if (!TryParseVersion(tag, out var latest) || !TryParseVersion(VersionText, out var current))
            {
                _updateStatusText = "已获取发布信息，但版本号无法比较";
                return;
            }

            if (latest > current)
            {
                _isUpdateAvailable = true;
                _updateStatusText = $"发现新版本 {title}";
            }
            else
            {
                _updateStatusText = $"已是最新版本（{VersionText}）";
            }
        }
        catch (TaskCanceledException)
        {
            _updateStatusText = "检查更新超时，请稍后重试";
        }
        catch (Exception ex)
        {
            _updateStatusText = "检查更新失败：" + ex.Message;
        }
        finally
        {
            _isCheckingUpdate = false;
            NotifyUpdateState();
        }
    }

    public void OpenUpdateDownload()
    {
        if (string.IsNullOrWhiteSpace(_updateDownloadUrl))
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(_updateDownloadUrl) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            ShowNotice("打开下载页面失败：" + ex.Message);
        }
    }

    public async Task CheckForUpdatesOnStartupAsync()
    {
        await Task.Delay(TimeSpan.FromSeconds(3));
        await CheckForUpdatesAsync();
    }
    public async Task DownloadAndInstallUpdateAsync()
    {
        if (string.IsNullOrWhiteSpace(_updateDownloadUrl) ||
            !_updateDownloadUrl.EndsWith(".msi", StringComparison.OrdinalIgnoreCase))
        {
            OpenUpdateDownload();
            return;
        }

        var fileName = Path.GetFileName(new Uri(_updateDownloadUrl).LocalPath);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            fileName = $"9mRoom-Setup-{VersionText}.msi";
        }

        var target = Path.Combine(Path.GetTempPath(), fileName);
        _updateStatusText = "正在下载更新…";
        NotifyUpdateState();

        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
            using var response = await http.GetAsync(_updateDownloadUrl, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            await using (var source = await response.Content.ReadAsStreamAsync())
            await using (var destination = File.Create(target))
            {
                await source.CopyToAsync(destination);
            }

            _updateStatusText = "更新下载完成，等待安装确认";
            NotifyUpdateState();

            var result = System.Windows.MessageBox.Show(
                $"更新已下载完成：\n{target}\n\n是否现在更新？安装程序会关闭 9mRoom。",
                "9mRoom 更新",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes)
            {
                return;
            }

            Process.Start(new ProcessStartInfo("msiexec.exe", $"/i \"{target}\"")
            {
                UseShellExecute = true
            });
            WpfApplication.Current.Shutdown();
        }
        catch (Exception ex)
        {
            _updateStatusText = "更新下载失败：" + ex.Message;
            NotifyUpdateState();
        }
    }

    private void NotifyUpdateState()
    {
        OnPropertyChanged(nameof(IsCheckingUpdate));
        OnPropertyChanged(nameof(IsUpdateAvailable));
        OnPropertyChanged(nameof(UpdateStatusText));
    }

    private static bool TryParseVersion(string text, out Version version)
    {
        var match = Regex.Match(text ?? string.Empty, @"\d+(?:\.\d+){1,3}");
        if (!match.Success)
        {
            version = null!;
            return false;
        }

        return Version.TryParse(match.Value, out version!);
    }

    public string SettingsPathText => string.IsNullOrEmpty(SettingsStore.CurrentPath)
        ? "尚未写入（退出时保存）"
        : SettingsStore.CurrentPath;

    public event Action<ToastRequest>? ToastRequested;

    public event Action? OverlayVisibilityChanged;

    public event Action? OverlayClickThroughChanged;

    public event Action? OverlayThemeChanged;

    public event Action? OverlayPositionResetRequested;

    public void RequestOverlayReset() => OverlayPositionResetRequested?.Invoke();

    public void ShowNoticeText(string text) => ShowNotice(text);

    public void ShowPersistentNotice(string text)
    {
        _noticeTimer?.Stop();
        PersistentNoticeText = text;
    }

    /// <summary>
    /// 长期有效的系统状态（例如托盘不可用）。这类信息不能用 Snackbar 表达，
    /// 由主窗口以常驻提示条显示。
    /// </summary>
    public string PersistentNoticeText
    {
        get => _persistentNoticeText;
        private set => SetProperty(ref _persistentNoticeText, value);
    }

    /// <summary>连接/断开进行中：按钮进入 busy 状态，避免重复提交。</summary>
    public bool IsConnecting
    {
        get => _isConnecting;
        private set => SetProperty(ref _isConnecting, value);
    }

    public bool IsServerConnected => _isServerConnected;

    public bool IsInRoom => _client is not null;

    public bool IsNotInRoom => !IsInRoom;

    public bool IsConnected => _isServerConnected || _client?.IsConnected == true;

    public string ConnectionState => _connectionState;

    public string ConnectionText
    {
        get
        {
            if (!_isServerConnected)
            {
                return "未连接";
            }

            if (_client is null)
            {
                return "已连接服务器 · 未进入房间";
            }

            return _client.IsConnected
                ? $"已连接 · {_client.OnlineCount} 人在线"
                : "已断开，正在重连…";
        }
    }

    public string ConnectButtonText => _isServerConnected ? "断开服务器" : "连接服务器";

    public bool IsJoinDialogOpen
    {
        get => _isJoinDialogOpen;
        set => SetProperty(ref _isJoinDialogOpen, value);
    }

    public string JoinRoomCode
    {
        get => _joinRoomCode;
        set
        {
            if (!SetProperty(ref _joinRoomCode, value ?? string.Empty))
            {
                return;
            }

            _joinInfoDebounce ??= CreateJoinInfoDebounce();
            _joinInfoDebounce.Stop();
            _joinInfoDebounce.Start();
        }
    }

    public string JoinRoomName
    {
        get => _joinRoomName;
        set => SetProperty(ref _joinRoomName, value ?? string.Empty);
    }

    public string JoinRoomInfoText
    {
        get => _joinRoomInfoText;
        private set => SetProperty(ref _joinRoomInfoText, value);
    }

    public bool JoinRememberName
    {
        get => _joinRememberName;
        set => SetProperty(ref _joinRememberName, value);
    }

    public bool JoinCanSetHome
    {
        get => _joinCanSetHome;
        private set => SetProperty(ref _joinCanSetHome, value);
    }

    public bool JoinSetHome
    {
        get => _joinSetHome;
        set
        {
            if (value && !_joinCanSetHome)
            {
                return;
            }

            SetProperty(ref _joinSetHome, value);
        }
    }

    public void OpenJoinDialog()
    {
        if (!_isServerConnected)
        {
            ShowNotice("请先在状态页连接服务器。");
            return;
        }

        JoinRoomCode = _settings.HomeRoom;
        JoinRoomName = string.IsNullOrWhiteSpace(_settings.Name)
            ? Environment.UserName
            : _settings.Name;
        JoinRememberName = true;
        JoinCanSetHome = false;
        JoinSetHome = false;
        JoinRoomInfoText = "输入房间号";
        IsJoinDialogOpen = true;
    }

    public void CloseJoinDialog() => IsJoinDialogOpen = false;

    public string MemberCountText => $"{Members.Count} 人";

    public async Task InitializeAsync()
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        _bootTime = SystemInfo.BootTimeMs();

        if (_settings.AutoStart && !AutoStartService.IsEnabled())
        {
            _settings.AutoStart = false;
            SettingsStore.Save(_settings);
        }

        try
        {
            _media = await MediaWatcher.CreateAsync();
        }
        catch
        {
            _media = null;
        }

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _timer.Tick += (_, _) => Refresh();
        _timer.Start();
        Refresh();

        if (_settings.AutoConnect)
        {
            if (!string.IsNullOrWhiteSpace(_settings.HomeRoom))
            {
                var homeInfo = await GetRoomInfoAsync(_settings.HomeRoom);
                if (homeInfo is { Exists: false } ||
                    homeInfo is { Exists: true, Kind: not "server" })
                {
                    _settings.HomeRoom = string.Empty;
                    SettingsStore.Save(_settings);
                    ShowNotice("家房间已不存在，已清除。请手动加入房间。");
                    Refresh();
                    _settingsToastEnabled = true;
                    return;
                }
            }

            await ToggleConnectionAsync();
        }

        _settingsToastEnabled = true;
    }

    public async Task SendChatAsync()
    {
        var text = ChatInput.Trim();
        if (text.Length == 0)
        {
            return;
        }

        if (_client is not { IsConnected: true })
        {
            MyStatusText = "未连接，无法发送消息";
            return;
        }

        ChatInput = string.Empty;
        try
        {
            await _client.SendChatAsync(text);
        }
        catch (Exception ex)
        {
            MyStatusText = $"发送失败：{ex.Message}";
        }
    }

    private void OnRoomMembersChanged()
    {
        var members = _client?.Members ?? Array.Empty<RoomMember>();

        if (!_roomLogInitialized)
        {
            _roomLogInitialized = true;
            AddRoomLog($"已进入房间 {RoomDisplayName}", "system");
            foreach (var member in members.Where(member => member.Id != _client?.SelfId))
            {
                AddRoomLog($"{member.Name} 已在房间", "join");
            }
        }
        else
        {
            var currentIds = members.Select(member => member.Id).ToHashSet(StringComparer.Ordinal);

            foreach (var member in members)
            {
                if (!_roomMemberLog.TryGetValue(member.Id, out var old))
                {
                    AddRoomLog($"{member.Name} 加入了房间", "join");
                    if (member.Id != _client?.SelfId && member.Online && _settings.NotifyRoomEvents)
                    {
                        NotifySystem("9mRoom", $"{member.Name} 进入了房间");
                    }

                    continue;
                }

                if (!string.Equals(old.Name, member.Name, StringComparison.Ordinal))
                {
                    AddRoomLog($"{old.Name} 改名为 {member.Name}", "rename");
                }

                if (!string.Equals(old.Mode, member.Mode, StringComparison.Ordinal))
                {
                    AddRoomLog($"{member.Name} {ModeLogText(member.Mode)}", "mode");
                }
            }

            foreach (var old in _roomMemberLog.Where(pair => !currentIds.Contains(pair.Key)))
            {
                AddRoomLog($"{old.Value.Name} 离开了房间", "leave");
                if (old.Key != _client?.SelfId && _settings.NotifyRoomEvents)
                {
                    NotifySystem("9mRoom", $"{old.Value.Name} 离开了房间");
                }
            }
        }

        _roomMemberLog.Clear();
        foreach (var member in members)
        {
            _roomMemberLog[member.Id] = (member.Name, member.Mode, member.Online);
        }

        SyncMembers();
        Refresh();
    }

    private void AddRoomLog(string text, string kind)
    {
        RoomLogs.Insert(0, new RoomLogItem(DateTime.Now.ToString("HH:mm:ss"), text, kind));
        while (RoomLogs.Count > 300)
        {
            RoomLogs.RemoveAt(RoomLogs.Count - 1);
        }
    }

    private static string ModeLogText(string mode) => mode switch
    {
        "focusing" => "开始专注",
        "break" => "开始休息",
        "away" => "暂时离开",
        "invisible" => "进入隐身",
        "offline" => "离线",
        "auto" => "恢复自动状态",
        _ => "恢复了在线"
    };

    public async Task ToggleConnectionAsync()
    {
        if (IsConnecting)
        {
            return;
        }

        IsConnecting = true;
        try
        {
            if (_isServerConnected)
            {
                await DisconnectServerAsync();
                return;
            }

            await ConnectServerAsync();
        }
        finally
        {
            IsConnecting = false;
        }
    }

    private async Task ConnectServerAsync()
    {
        var baseUrl = ServerBase.Trim().TrimEnd('/');
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != "http" && uri.Scheme != "https"))
        {
            ShowNotice("服务器地址不合法，请填写 http:// 或 https:// 开头的地址。");
            return;
        }

        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            using var response = await http.GetAsync(baseUrl + "/healthz");
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"服务器返回 {(int)response.StatusCode}");
            }

            _isServerConnected = true;
            MyStatusText = "已连接服务器，尚未进入房间";
            NotifyConnectionState();
            NotifyToast("已连接服务器", "服务器连接成功。", ToastKind.Success, 3000);

            if (!string.IsNullOrWhiteSpace(_settings.HomeRoom))
            {
                var name = string.IsNullOrWhiteSpace(_settings.Name)
                    ? Environment.UserName
                    : _settings.Name;
                var info = await GetRoomInfoAsync(_settings.HomeRoom);
                if (info is { Exists: true, Kind: "server" })
                {
                    await JoinRoomAsync(_settings.HomeRoom, name, rememberName: false, setHome: false);
                }
                else if (info is { Exists: false })
                {
                    _settings.HomeRoom = string.Empty;
                    SettingsStore.Save(_settings);
                    ShowNotice("家房间已不存在，已清除。");
                }
            }
        }
        catch (Exception ex)
        {
            _isServerConnected = false;
            MyStatusText = "未连接";
            ShowNotice($"连接服务器失败：{ex.Message}");
            NotifyToast("无法连接服务器", ex.Message, ToastKind.Error, 7000);
            NotifyConnectionState();
        }
    }

    private async Task DisconnectServerAsync()
    {
        await LeaveRoomAsync();
        _isServerConnected = false;
        MyStatusText = "未连接";
        NotifyConnectionState();
        NotifyToast("已断开服务器", "服务器连接已关闭。", ToastKind.Info, 3000);
    }

    public async Task LeaveRoomAsync()
    {
        var wasInRoom = _client is not null || _joinedRoom.Length > 0;
        _wantsConnection = false;
        _reconnectTimer?.Stop();
        _reconnectAttempt = 0;

        if (_client is not null)
        {
            var client = _client;
            _client = null;
            try
            {
                await client.SendLeaveAsync();
            }
            catch
            {
                // 断线时也继续清理本地状态
            }

            await client.DisposeAsync();
        }

        ClearMembers();
        RoomLogs.Clear();
        ChatMessages.Clear();
        _roomMemberLog.Clear();
        _roomLogInitialized = false;
        _joinedRoom = string.Empty;
        _joinedName = string.Empty;
        ShowRoomCodeNotice(string.Empty);
        NotifyConnectionState();
        Refresh();

        if (wasInRoom)
        {
            NotifyToast("已离开房间", "你已退出当前房间。", ToastKind.Info, 3000);
        }
    }

    private DispatcherTimer CreateJoinInfoDebounce()
    {
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
        timer.Tick += async (_, _) =>
        {
            timer.Stop();
            await RefreshJoinRoomInfoAsync();
        };
        return timer;
    }

    private async Task RefreshJoinRoomInfoAsync()
    {
        var code = JoinRoomCode.Trim().ToLowerInvariant();
        if (code.Length == 0)
        {
            JoinRoomInfoText = "输入房间号";
            JoinCanSetHome = false;
            JoinSetHome = false;
            return;
        }

        var info = await GetRoomInfoAsync(code);
        if (code != JoinRoomCode.Trim().ToLowerInvariant())
        {
            return;
        }

        if (info is null)
        {
            JoinCanSetHome = false;
            JoinSetHome = false;
            JoinRoomInfoText = "无法检查房间信息，仍可尝试加入";
            return;
        }

        if (info.Value.Exists && info.Value.Kind == "server")
        {
            JoinCanSetHome = true;
            JoinRoomInfoText = $"服务端房间 · {info.Value.Online} 人在线 · 可设为家";
            return;
        }

        JoinCanSetHome = false;
        JoinSetHome = false;
        JoinRoomInfoText = info.Value.Exists
            ? "用户创建的房间 · 不能设为家"
            : "房间不存在，请向管理员确认房间号";
    }

    private async Task<(bool Exists, string Kind, int Online, string Name)?> GetRoomInfoAsync(string room)
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(4) };
            var url = $"{ServerBase.TrimEnd('/')}/api/rooms/{Uri.EscapeDataString(room)}";
            var json = await http.GetStringAsync(url);
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            var exists = root.TryGetProperty("exists", out var existsElement) && existsElement.GetBoolean();
            var kind = root.TryGetProperty("kind", out var kindElement)
                ? kindElement.GetString() ?? "user"
                : "user";
            var online = root.TryGetProperty("online", out var onlineElement) &&
                         onlineElement.ValueKind == JsonValueKind.Number &&
                         onlineElement.TryGetInt32(out var count)
                ? count
                : 0;
            var name = root.TryGetProperty("name", out var nameElement)
                ? nameElement.GetString() ?? string.Empty
                : string.Empty;
            return (exists, kind, online, name);
        }
        catch
        {
            return null;
        }
    }

    public async Task ConfirmJoinAsync()
    {
        var room = JoinRoomCode.Trim().ToLowerInvariant();
        var name = JoinRoomName.Trim();

        if (room.Length is 0 or > 64)
        {
            JoinRoomInfoText = "房间号不能为空，且不超过 64 个字符";
            return;
        }

        if (name.Length is 0 or > 24)
        {
            JoinRoomInfoText = "昵称需要 1–24 个字符";
            return;
        }

        var setHome = JoinSetHome && JoinCanSetHome;
        var ok = await JoinRoomAsync(room, name, JoinRememberName, setHome);
        if (ok && IsInRoom)
        {
            CloseJoinDialog();
        }
    }

    private async Task<bool> JoinRoomAsync(string room, string name, bool rememberName, bool setHome)
    {
        if (!_isServerConnected)
        {
            JoinRoomInfoText = "请先连接服务器";
            ShowNotice("请先在状态页连接服务器。");
            return false;
        }

        room = room.Trim().ToLowerInvariant();
        name = name.Trim();
        if (room.Length == 0 || name.Length == 0)
        {
            return false;
        }

        if (_client is not null)
        {
            await LeaveRoomAsync();
        }

        var info = await GetRoomInfoAsync(room);
        if (info is null)
        {
            JoinRoomInfoText = "无法检查房间信息，请确认服务器可用";
            return false;
        }

        if (!info.Value.Exists)
        {
            JoinRoomInfoText = "房间不存在，请向管理员确认房间号";
            if (string.Equals(_settings.HomeRoom, room, StringComparison.OrdinalIgnoreCase))
            {
                _settings.HomeRoom = string.Empty;
                SettingsStore.Save(_settings);
            }

            _settings.RoomName = string.Empty;
            OnPropertyChanged(nameof(RoomDisplayName));
            return false;
        }

        var serverRoom = info.Value.Kind == "server";
        if (setHome && !serverRoom)
        {
            setHome = false;
        }

        if (rememberName)
        {
            _settings.Name = name;
            OnPropertyChanged(nameof(Name));
        }

        _settings.Room = room;
        SetRoomName(info.Value.Name);
        ShowRoomCodeNotice(string.Empty);
        if (setHome)
        {
            _settings.HomeRoom = room;
        }

        SettingsStore.Save(_settings);

        _joinedRoom = room;
        _joinedName = name;
        await ConnectRoomAsync(room, name);

        if (_client is not { IsConnected: true })
        {
            JoinRoomInfoText = "加入房间失败，请检查服务器状态";
            return false;
        }

        NotifyConnectionState();
        if (setHome)
        {
            ShowNotice($"已把 {room} 设为家房间");
        }

        return true;
    }

    private async Task ConnectRoomAsync(string room, string name)
    {
        if (_client is not null)
        {
            return;
        }

        _wantsConnection = true;
        _reconnectAttempt = 0;
        NotifyConnectionState();
        var client = new PresenceClient(BuildUri(room, name), BuildPayload);
        client.Paused = _paused;
        _client = client;
        RoomLogs.Clear();
        ChatMessages.Clear();
        _roomMemberLog.Clear();
        _roomLogInitialized = false;
        client.StateChanged += () => _dispatcher.Invoke(OnClientStateChanged);
        client.MembersChanged += () => _dispatcher.Invoke(OnRoomMembersChanged);
        client.ChatReceived += message => _dispatcher.Invoke(() =>
        {
            var isSelf = string.Equals(message.MemberId, client.SelfId, StringComparison.Ordinal);
            ChatMessages.Add(new ChatMessageItem(
                message.Id,
                message.Name,
                message.Device,
                message.Text,
                DateTimeOffset.FromUnixTimeMilliseconds(message.At).ToLocalTime().ToString("HH:mm"),
                isSelf));

            while (ChatMessages.Count > 500)
            {
                ChatMessages.RemoveAt(0);
            }

            if (!isSelf && _settings.NotifyRoomChat)
            {
                var preview = (message.Text ?? string.Empty)
                    .Replace('\r', ' ')
                    .Replace('\n', ' ')
                    .Trim();
                if (preview.Length > 100)
                {
                    preview = preview[..100] + "…";
                }

                NotifySystem("9mRoom · 房间聊天", $"{message.Name}：{preview}");
            }
        });
        client.Message += message => _dispatcher.Invoke(() => MyStatusText = message);
        client.PokeReceived += poke => _dispatcher.Invoke(() =>
        {
            var name = string.IsNullOrWhiteSpace(poke.From.Name) ? "有人" : poke.From.Name;
            PokeStatusText = $"{name} 戳了你一下！";
            NotifySystem("9mRoom", $"{name} 戳了你一下！");
        });

        try
        {
            await client.ConnectAsync(CancellationToken.None);
            await client.UpdateNameAsync(name);
            if (_avatar is not null)
            {
                await client.SendAvatarAsync(_avatar);
            }

            _reconnectAttempt = 0;
        }
        catch (WebSocketException ex) when (
            ex.Message.Contains("404", StringComparison.Ordinal) ||
            ex.Message.Contains("room_not_found", StringComparison.OrdinalIgnoreCase))
        {
            _client = null;
            await client.DisposeAsync();
            _wantsConnection = false;
            _joinedRoom = string.Empty;
            _settings.RoomName = string.Empty;
            OnPropertyChanged(nameof(RoomDisplayName));
            if (string.Equals(_settings.HomeRoom, room, StringComparison.OrdinalIgnoreCase))
            {
                _settings.HomeRoom = string.Empty;
                SettingsStore.Save(_settings);
            }

            JoinRoomInfoText = "房间不存在或已被管理员删除";
            MyStatusText = "房间不存在或已被管理员删除";
            ShowNotice("房间不存在或已被管理员删除。");
        }
        catch (Exception ex)
        {
            _client = null;
            await client.DisposeAsync();
            MyStatusText = $"加入房间失败：{ex.Message}";
            ScheduleReconnect();
        }

        NotifyConnectionState();
        Refresh();
    }

    public event Action<string, string>? SystemNotificationRequested;

    public bool NotifyRoomEvents
    {
        get => _settings.NotifyRoomEvents;
        set
        {
            if (_settings.NotifyRoomEvents == value)
            {
                return;
            }

            _settings.NotifyRoomEvents = value;
            SettingsStore.Save(_settings);
            OnPropertyChanged();
        }
    }

    public bool NotifyRoomChat
    {
        get => _settings.NotifyRoomChat;
        set
        {
            if (_settings.NotifyRoomChat == value)
            {
                return;
            }

            _settings.NotifyRoomChat = value;
            SettingsStore.Save(_settings);
            OnPropertyChanged();
        }
    }

    private void NotifySystem(string title, string text)
    {
        SystemNotificationRequested?.Invoke(title, text);
    }

    private void NotifyConnectionState()
    {
        OnPropertyChanged(nameof(IsServerConnected));
        OnPropertyChanged(nameof(IsInRoom));
        OnPropertyChanged(nameof(IsNotInRoom));
        OnPropertyChanged(nameof(IsConnected));
        OnPropertyChanged(nameof(ConnectionText));
        var state = _client is { IsConnected: true }
            ? "connected"
            : _wantsConnection
                ? "reconnecting"
                : "offline";
        if (!string.Equals(_connectionState, state, StringComparison.Ordinal))
        {
            _connectionState = state;
            OnPropertyChanged(nameof(ConnectionState));
        }

        OnPropertyChanged(nameof(ConnectButtonText));
        OnPropertyChanged(nameof(MemberCountText));
    }

    private void OnClientStateChanged()
    {
        if (_client is { IsConnected: true } && _wasReconnecting)
        {
            _wasReconnecting = false;
            NotifyToast("已重新连接", "房间连接已恢复。", ToastKind.Success, 3500);
        }

        if (_client is { IsConnected: false } dead && _wantsConnection)
        {
            _client = null;
            _ = dead.DisposeAsync();
            NotifyConnectionState();
            ScheduleReconnect();
        }

        Refresh();
    }

    private void ScheduleReconnect()
    {
        if (!_wantsConnection || _joinedRoom.Length == 0)
        {
            return;
        }

        if (!_wasReconnecting)
        {
            _wasReconnecting = true;
            NotifyToast("连接已断开", "网络或服务器异常，正在尝试重新连接…", ToastKind.Warning, 5000);
        }

        if (_reconnectTimer is null)
        {
            _reconnectTimer = new DispatcherTimer();
            _reconnectTimer.Tick += async (_, _) =>
            {
                _reconnectTimer!.Stop();
                if (_client is null && _joinedRoom.Length > 0)
                {
                    await ConnectRoomAsync(_joinedRoom, _joinedName);
                }
            };
        }

        _reconnectTimer.Stop();
        var seconds = Math.Min(60, Math.Pow(2, Math.Min(_reconnectAttempt, 6)));
        _reconnectAttempt++;
        _reconnectTimer.Interval = TimeSpan.FromSeconds(seconds);
        _reconnectTimer.Start();
        NotifyConnectionState();
    }

    public void Refresh()
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var foreground = _foreground.Current;
        var match = foreground is null
            ? new AppMatch("unknown", "unknown", "other")
            : _catalog.Match(foreground.ProcessName);

        var appRule = foreground is null ? null : EnsureAppRule(match, foreground);
        if (appRule is not null && !string.IsNullOrWhiteSpace(appRule.CategoryOverride))
        {
            match = match with { Category = appRule.CategoryOverride };
        }

        if (foreground is not null && appRule is not null &&
            !string.Equals(_lastDetectedAppId, match.Id, StringComparison.Ordinal))
        {
            _lastDetectedAppId = match.Id;
            appRule.LastSeenAt = DateTimeOffset.UtcNow;
            if (AppSortMode == "recent")
            {
                ApplyAppSort();
            }

            _layoutDebounce?.Stop();
            _layoutDebounce?.Start();
        }

        var media = SharedMedia();
        var idle = SystemInfo.IdleTime();
        UpdateTimer(now);
        TrackStats(idle, now);

        MyAppText = foreground is null
            ? "-"
            : $"{ResolveAppName(match.Id)} · {CategoryName(match.Category)}";
        MyMediaText = media is null || string.IsNullOrEmpty(media.Title)
            ? "未在播放"
            : $"{media.Title} - {media.Artist}";
        MyIdleText = $"{(int)idle.TotalSeconds} 秒";

        if (_manualMode == "invisible")
        {
            MyStatusText = "隐身中（不对房间展示状态）";
        }
        else
        {
            var timer = _timerRunning
                ? new FocusTimer("countdown", _timerStartedAt, PhaseMinutes)
                : null;
            var todo = ActiveTodoText;
            var sharedWindowTitle = SharedWindowTitle(appRule, match.Id);
            var status = StatusComposer.Compose(
                match,
                media,
                idle,
                _bootTime,
                timer,
                todo,
                _commandOutput,
                CurrentLayout(match.Id, match.Category),
                ResolveAppName(match.Id),
                sharedWindowTitle);
            MyStatusText = RenderStatus(status, now);
        }

        if (!IsInRoom)
        {
            MyStatusText = _isServerConnected ? "未进入房间" : "未连接";
        }

        var lastSend = _client?.LastSendAt ?? 0;
        LastSyncText = _client is { IsConnected: true } && lastSend > 0
            ? $"同步于 {Math.Max(0, (now - lastSend) / 1000)} 秒前"
            : "未同步";

        var modeText = _manualMode != "auto"
            ? ManualModeText.Replace("手动：", string.Empty)
            : idle >= TimeSpan.FromMinutes(5) ? "离开" : "专注中";

        TrayText = _paused
            ? "9mRoom · 已暂停上报"
            : _client is { IsConnected: true } connected
                ? $"9mRoom · {RoomDisplayName} · {connected.OnlineCount} 人在线 · {modeText}"
                : _isServerConnected
                    ? "9mRoom · 已连接服务器 · 未进入房间"
                    : "9mRoom · 未连接";

        var prefix = _paused ? "已暂停 · " : string.Empty;
        OverlayHeader = _client is { IsConnected: true } online
            ? $"{prefix}{RoomDisplayName} · {online.OnlineCount} 人在线 · {modeText}"
            : _isServerConnected
                ? $"{prefix}9mRoom · 未进入房间"
                : $"{prefix}9mRoom · 未连接";

        if (_client is not null)
        {
            _client.SendInterval = _manualMode == "invisible"
                ? TimeSpan.FromSeconds(30)
                : _manualMode == "auto" && idle >= TimeSpan.FromMinutes(5)
                    ? TimeSpan.FromSeconds(15)
                    : TimeSpan.FromSeconds(3);
        }

        if (_client is not null)
        {
            foreach (var member in _client.Members)
            {
                if (_memberMap.TryGetValue(member.Id, out var item))
                {
                    item.ConnectionState = member.ConnectionState;
                    item.Avatar = GetAvatarImage(member);
                    item.StatusText = member.ConnectionState == "reconnecting"
                        ? "重连中"
                        : member.Mode == "invisible"
                            ? "隐身中"
                            : StatusRenderer.Render(member.Status, now, ResolveAppName);
                    item.AgoText = AgoText(now - member.UpdatedAt);
                    item.IsOnline = member.Online && _client.IsConnected;
                }
            }
        }

        OnPropertyChanged(nameof(IsConnected));
        OnPropertyChanged(nameof(IsInRoom));
        OnPropertyChanged(nameof(IsNotInRoom));
        OnPropertyChanged(nameof(IsServerConnected));
        OnPropertyChanged(nameof(ConnectionText));
        OnPropertyChanged(nameof(ConnectButtonText));
        OnPropertyChanged(nameof(MemberCountText));
    }

    private void SyncMembers()
    {
        var members = (_client?.Members ?? Array.Empty<RoomMember>())
            .ToArray();
        var live = new HashSet<string>(members.Select(member => member.Id));

        foreach (var id in _memberMap.Keys.Where(id => !live.Contains(id)).ToArray())
        {
            var removed = _memberMap[id];
            if (ReferenceEquals(_selectedMember, removed))
            {
                CloseMemberDrawer();
                SelectedMember = null;
            }

            Members.Remove(removed);
            _memberMap.Remove(id);
        }

        foreach (var member in members)
        {
            if (!_memberMap.TryGetValue(member.Id, out var item))
            {
                item = new MemberItem { Id = member.Id };
                _memberMap[member.Id] = item;
                Members.Add(item);
            }

            item.Name = member.Name;
            item.DeviceLabel = member.Device == "android" ? "Android" : "Windows";
            item.Initial = string.IsNullOrEmpty(member.Name) ? "?" : member.Name[..1];
            item.IsOnline = member.Online;
            item.ConnectionState = member.ConnectionState;
            item.Avatar = GetAvatarImage(member);
            item.CanPoke = !string.Equals(member.Id, _client?.SelfId, StringComparison.Ordinal) &&
                           member.Online &&
                           member.ConnectionState != "reconnecting";
            item.WindowTitle = member.WindowTitle;
            item.ModeText = member.Mode switch
            {
                "focusing" => "专注",
                "break" => "休息",
                "away" => "离开",
                "invisible" => "隐身",
                _ => member.Mode
            };
            item.TodayFocusText = FormatDuration(member.FocusSecondsToday);
        }
    }

    private void ClearMembers()
    {
        Members.Clear();
        _memberMap.Clear();
        _avatarImageCache.Clear();
    }

    private ImageSource? GetAvatarImage(RoomMember member)
    {
        var avatar = member.Avatar;
        if (avatar is null || string.IsNullOrWhiteSpace(avatar.Hash))
        {
            return null;
        }

        if (_avatarImageCache.TryGetValue(avatar.Hash, out var cached))
        {
            return cached;
        }

        var image = AvatarStore.ToImage(avatar, 128);
        if (image is not null)
        {
            _avatarImageCache[avatar.Hash] = image;
        }

        return image;
    }

    private object BuildPayload()
    {
        var foreground = _foreground.Current;
        var match = foreground is null
            ? new AppMatch("unknown", "unknown", "other")
            : _catalog.Match(foreground.ProcessName);

        var appRule = FindAppRule(match.Id);
        if (appRule is not null && !string.IsNullOrWhiteSpace(appRule.CategoryOverride))
        {
            match = match with { Category = appRule.CategoryOverride };
        }

        var idle = SystemInfo.IdleTime();
        var mode = _manualMode == "auto" ? StatusComposer.Mode(idle) : _manualMode;

        var payload = new Dictionary<string, object?> { ["mode"] = mode };
        if (mode != "invisible")
        {
            var timer = _timerRunning
                ? new FocusTimer("countdown", _timerStartedAt, PhaseMinutes)
                : null;
            var todo = ActiveTodoText;

            var sharedWindowTitle = SharedWindowTitle(appRule, match.Id);

            payload["status"] = StatusComposer.Compose(
                match,
                SharedMedia(),
                idle,
                _bootTime,
                timer,
                todo,
                _commandOutput,
                CurrentLayout(match.Id, match.Category),
                ResolveAppName(match.Id),
                sharedWindowTitle);

            if (sharedWindowTitle is not null)
            {
                payload["windowTitle"] = sharedWindowTitle;
            }

            payload["focusSecondsToday"] = _settings.StatsEnabled ? TodaySeconds() : 0;
        }

        return payload;
    }

    private string? SharedWindowTitle(AppRuleItem? appRule, string appId)
    {
        var windowTitle = _foreground.Current?.WindowTitle;
        if (!_settings.ShareWindowTitles ||
            string.IsNullOrWhiteSpace(windowTitle) ||
            appRule?.HideTitle == true ||
            TitleWhitelist.Any(item => string.Equals(item.AppId, appId, StringComparison.Ordinal)))
        {
            return null;
        }

        return windowTitle.Trim();
    }

    private MediaInfo? SharedMedia()
    {
        var media = _media?.Current;
        if (!_settings.ShareMusic || media is null)
        {
            return null;
        }

        var filters = (_settings.MediaSourceFilter ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (filters.Length == 0)
        {
            return media;
        }

        return filters.Any(filter =>
            media.SourceAppId.Contains(filter, StringComparison.OrdinalIgnoreCase))
            ? media
            : null;
    }

    private Uri BuildUri(string room, string name)
    {
        var builder = new UriBuilder(ServerBase) { Path = "/v1/ws" };
        builder.Scheme = builder.Scheme == "https" ? "wss" : "ws";
        var query = new Dictionary<string, string>
        {
            ["room"] = room,
            ["name"] = name,
            ["client"] = _settings.ClientId,
            ["device"] = "windows",
            ["role"] = "member"
        };
        builder.Query = string.Join("&", query.Select(pair => $"{pair.Key}={Uri.EscapeDataString(pair.Value)}"));
        return builder.Uri;
    }

    private string RenderStatus(object status, long now)
    {
        var json = JsonSerializer.Serialize(status);
        using var document = JsonDocument.Parse(json);
        return StatusRenderer.Render(document.RootElement, now, ResolveAppName);
    }

    private string ResolveAppName(string appId)
    {
        var rule = FindAppRule(appId);
        return rule is not null && !string.IsNullOrWhiteSpace(rule.DisplayName)
            ? rule.DisplayName
            : _catalog.DisplayName(appId);
    }

    private static string AgoText(long milliseconds)
    {
        var seconds = Math.Max(0, milliseconds / 1000);
        return seconds switch
        {
            < 5 => "刚刚",
            < 60 => $"{seconds} 秒前",
            _ => $"{seconds / 60} 分钟前"
        };
    }

    private void NotifyToast(string title, string message, ToastKind kind = ToastKind.Info, int durationMs = 5000) =>
        ToastRequested?.Invoke(new ToastRequest(title, message, kind, durationMs));

    private void OnSettingsSaved(bool saved)
    {
        if (!_settingsToastEnabled)
        {
            return;
        }

        if (saved)
        {
            NotifyToast("设置已保存", "更改已写入本地配置。", ToastKind.Success, 2200);
        }
        else
        {
            NotifyToast("设置保存失败", "无法写入本地配置文件，请检查目录权限。", ToastKind.Error, 8000);
        }
    }

    private void ShowNotice(string text)
    {
        NoticeText = text;
        _noticeTimer?.Stop();
        _noticeTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(6) };
        _noticeTimer.Tick += (_, _) =>
        {
            _noticeTimer!.Stop();
            NoticeText = string.Empty;
        };
        _noticeTimer.Start();
    }

    public void Dispose()
    {
        SettingsStore.Saved -= OnSettingsSaved;
        PersistStats();
        _commandTimer?.Stop();
        _nameDebounce?.Stop();
        _wantsConnection = false;
        _timer?.Stop();
        _reconnectTimer?.Stop();
        _foreground.Dispose();

        if (_client is not null)
        {
            _ = _client.DisposeAsync();
            _client = null;
        }

        if (_media is not null)
        {
            _ = _media.DisposeAsync();
            _media = null;
        }
    }
}
