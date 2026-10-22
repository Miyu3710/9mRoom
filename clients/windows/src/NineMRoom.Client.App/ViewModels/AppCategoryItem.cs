using System.Text.Json;

namespace NineMRoom.Client.App;

public sealed class AppCategoryItem : ObservableObject
{
    private readonly Action _changed;
    private string _name;
    private string _template;
    private bool _isDragging;

    public AppCategoryItem(string id, string name, string template, bool isDefault, Action changed)
    {
        Id = id;
        _name = name;
        _template = template;
        IsDefault = isDefault;
        _changed = changed;
    }

    public string Id { get; }

    public bool IsDefault { get; }

    public bool IsDragging
    {
        get => _isDragging;
        set => SetProperty(ref _isDragging, value);
    }

    public string Name
    {
        get => _name;
        set
        {
            var name = (value ?? string.Empty).Trim();
            if (string.Equals(_name, name, StringComparison.Ordinal))
            {
                return;
            }

            _name = name;
            OnPropertyChanged();
            _changed();
        }
    }

    public string Template
    {
        get => _template;
        set
        {
            var template = value ?? string.Empty;
            if (string.Equals(_template, template, StringComparison.Ordinal))
            {
                return;
            }

            _template = template;
            OnPropertyChanged();
            _changed();
        }
    }
}

public sealed class AppCategoryData
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Template { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
}
