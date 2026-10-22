namespace NineMRoom.Client.App;

public sealed class StatusBlockItem : ObservableObject
{
    private readonly Action _changed;
    private bool _enabled;
    private string _template;
    private bool _isDragging;

    public StatusBlockItem(string type, string label, string template, bool enabled, Action changed)
    {
        Type = type;
        Label = label;
        _template = template;
        _enabled = enabled;
        _changed = changed;
    }

    public string Type { get; }

    public string Label { get; }

    public bool Enabled
    {
        get => _enabled;
        set
        {
            if (_enabled == value)
            {
                return;
            }

            _enabled = value;
            OnPropertyChanged();
            _changed();
        }
    }

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

    public bool IsDragging
    {
        get => _isDragging;
        set => SetProperty(ref _isDragging, value);
    }
}
