namespace NineMRoom.Client.App;

public sealed class AppTemplateItem : ObservableObject
{
    private readonly Action _changed;
    private string _template;

    public AppTemplateItem(string appId, string displayName, string template, Action changed)
    {
        AppId = appId;
        DisplayName = displayName;
        _template = template;
        _changed = changed;
    }

    public string AppId { get; }

    public string DisplayName { get; }

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
}
