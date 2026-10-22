using System.Windows.Media;

namespace NineMRoom.Client.App;

public sealed class AppRuleItem : ObservableObject
{
    public static readonly string[] Categories =
    {
        "coding", "writing", "reading", "meeting", "media", "game", "browsing", "other"
    };

    private readonly Action _changed;
    private string _displayName;
    private string _template;
    private string _categoryOverride;
    private string _executablePath;
    private string _customIconPath;
    private bool _ignored;
    private bool _hideTitle;
    private bool _isSelected;
    private ImageSource? _icon;
    private DateTimeOffset _lastSeenAt;
    private string _categoryDisplayName = string.Empty;

    public AppRuleItem(
        string appId,
        string displayName,
        string processName,
        string defaultCategory,
        string template,
        string categoryOverride,
        bool ignored,
        bool hideTitle,
        Action changed,
        string executablePath = "",
        string customIconPath = "",
        DateTimeOffset lastSeenAt = default)
    {
        AppId = appId;
        _displayName = displayName;
        ProcessName = processName;
        DefaultCategory = defaultCategory;
        _template = template;
        _categoryOverride = categoryOverride;
        _ignored = ignored;
        _hideTitle = hideTitle;
        _changed = changed;
        _executablePath = executablePath;
        _customIconPath = customIconPath;
        _lastSeenAt = lastSeenAt;
    }

    public string AppId { get; }

    public string DisplayName
    {
        get => _displayName;
        set
        {
            var name = string.IsNullOrWhiteSpace(value) ? AppId : value.Trim();
            if (string.Equals(_displayName, name, StringComparison.Ordinal))
            {
                return;
            }

            _displayName = name;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Initial));
            _changed();
        }
    }

    public string Initial => string.IsNullOrEmpty(_displayName) ? "?" : _displayName[..1];

    public string ProcessName { get; set; }

    public string DefaultCategory { get; }

    public string ExecutablePath
    {
        get => _executablePath;
        set
        {
            if (string.Equals(_executablePath, value, StringComparison.Ordinal))
            {
                return;
            }

            _executablePath = value ?? string.Empty;
            OnPropertyChanged();
            _changed();
        }
    }

    public void UpdateExecutablePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) ||
            string.Equals(_executablePath, path, StringComparison.Ordinal))
        {
            return;
        }

        _executablePath = path;
        OnPropertyChanged(nameof(ExecutablePath));
    }

    public string CustomIconPath
    {
        get => _customIconPath;
        set
        {
            if (string.Equals(_customIconPath, value, StringComparison.Ordinal))
            {
                return;
            }

            _customIconPath = value ?? string.Empty;
            OnPropertyChanged();
            _changed();
        }
    }

    public ImageSource? Icon
    {
        get => _icon;
        set
        {
            if (SetProperty(ref _icon, value))
            {
                OnPropertyChanged(nameof(HasIcon));
            }
        }
    }

    public bool HasIcon => _icon is not null;

    public string Template
    {
        get => _template;
        set
        {
            var text = value ?? string.Empty;
            if (string.Equals(_template, text, StringComparison.Ordinal))
            {
                return;
            }

            _template = text;
            OnPropertyChanged();
            _changed();
        }
    }

    public string CategoryOverride
    {
        get => _categoryOverride;
        set
        {
            var category = value ?? string.Empty;
            if (string.Equals(_categoryOverride, category, StringComparison.Ordinal))
            {
                return;
            }

            _categoryOverride = category;
            OnPropertyChanged();
            OnPropertyChanged(nameof(EffectiveCategory));
            _changed();
        }
    }

    public string EffectiveCategory =>
        string.IsNullOrWhiteSpace(_categoryOverride) ? DefaultCategory : _categoryOverride;

    public string CategoryDisplayName
    {
        get => string.IsNullOrEmpty(_categoryDisplayName) ? EffectiveCategory : _categoryDisplayName;
        private set => SetProperty(ref _categoryDisplayName, value);
    }

    public void UpdateCategoryDisplayName(string name)
    {
        CategoryDisplayName = string.IsNullOrWhiteSpace(name) ? EffectiveCategory : name.Trim();
    }

    public bool Ignored
    {
        get => _ignored;
        set
        {
            if (_ignored == value)
            {
                return;
            }

            _ignored = value;
            OnPropertyChanged();
            _changed();
        }
    }

    public bool HideTitle
    {
        get => _hideTitle;
        set
        {
            if (_hideTitle == value)
            {
                return;
            }

            _hideTitle = value;
            OnPropertyChanged();
            _changed();
        }
    }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public DateTimeOffset LastSeenAt
    {
        get => _lastSeenAt;
        set => SetProperty(ref _lastSeenAt, value);
    }
}

public sealed class AppRuleData
{
    public string AppId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string ProcessName { get; set; } = string.Empty;
    public string DefaultCategory { get; set; } = "other";
    public string Template { get; set; } = string.Empty;
    public string CategoryOverride { get; set; } = string.Empty;
    public bool Ignored { get; set; }
    public bool HideTitle { get; set; }
    public string ExecutablePath { get; set; } = string.Empty;
    public string CustomIconPath { get; set; } = string.Empty;
    public DateTimeOffset LastSeenAt { get; set; }
}
