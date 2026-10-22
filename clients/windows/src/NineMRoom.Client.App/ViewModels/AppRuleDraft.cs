using System.Windows.Media;

namespace NineMRoom.Client.App;

public sealed class AppRuleDraft : ObservableObject
{
    private string _displayName;
    private string _executablePath;
    private string _template;
    private string _categoryOverride;
    private bool _ignored;
    private bool _hideTitle;
    private ImageSource? _icon;
    private bool _resetIcon;

    public AppRuleDraft(AppRuleItem item)
    {
        AppId = item.AppId;
        ProcessName = item.ProcessName;
        DefaultCategory = item.DefaultCategory;
        _executablePath = item.ExecutablePath;
        _displayName = item.DisplayName;
        _template = item.Template;
        _categoryOverride = item.CategoryOverride;
        _ignored = item.Ignored;
        _hideTitle = item.HideTitle;
        _icon = item.Icon;
    }

    public string AppId { get; }

    public string ProcessName { get; }

    public string DefaultCategory { get; }

    public string ExecutablePath
    {
        get => _executablePath;
        set => SetProperty(ref _executablePath, value ?? string.Empty);
    }

    public string DisplayName
    {
        get => _displayName;
        set
        {
            if (SetProperty(ref _displayName, value))
            {
                OnPropertyChanged(nameof(Initial));
            }
        }
    }

    public string Initial => string.IsNullOrEmpty(_displayName) ? "?" : _displayName[..1];

    public string Template
    {
        get => _template;
        set => SetProperty(ref _template, value);
    }

    public string CategoryOverride
    {
        get => _categoryOverride;
        set
        {
            if (SetProperty(ref _categoryOverride, value))
            {
                OnPropertyChanged(nameof(EffectiveCategory));
            }
        }
    }

    public string EffectiveCategory =>
        string.IsNullOrWhiteSpace(_categoryOverride) ? DefaultCategory : _categoryOverride;

    public bool Ignored
    {
        get => _ignored;
        set => SetProperty(ref _ignored, value);
    }

    public bool HideTitle
    {
        get => _hideTitle;
        set => SetProperty(ref _hideTitle, value);
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

    public string PendingIconPath { get; set; } = string.Empty;

    public bool ResetIcon
    {
        get => _resetIcon;
        set => SetProperty(ref _resetIcon, value);
    }
}
