using System.Collections.ObjectModel;
using System.ComponentModel;

namespace NineMRoom.Client.App;

public sealed class SubtaskItem : ObservableObject
{
    private string _title;
    private bool _isDone;

    public SubtaskItem(string title, bool isDone)
    {
        _title = title;
        _isDone = isDone;
    }

    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value ?? string.Empty);
    }

    public bool IsDone
    {
        get => _isDone;
        set => SetProperty(ref _isDone, value);
    }
}

public sealed class TodoItem : ObservableObject
{
    private readonly Action _changed;
    private string _title;
    private string _startTime;
    private string _endTime;
    private bool _isDone;
    private bool _isPinned;
    private bool _isEditing;
    private bool _suppressChildChange;

    public TodoItem(
        string id,
        string title,
        long createdAt,
        string startTime,
        string endTime,
        bool isDone,
        bool isPinned,
        IEnumerable<SubtaskItem> subtasks,
        Action changed)
    {
        Id = id;
        _title = title;
        CreatedAt = createdAt;
        _startTime = startTime ?? string.Empty;
        _endTime = endTime ?? string.Empty;
        _isDone = isDone;
        _isPinned = isPinned;
        _changed = changed;
        Subtasks = new ObservableCollection<SubtaskItem>(subtasks);
        foreach (var subtask in Subtasks)
        {
            subtask.PropertyChanged += Subtask_PropertyChanged;
        }
    }

    public string Id { get; }

    public long CreatedAt { get; }

    public ObservableCollection<SubtaskItem> Subtasks { get; }

    public string Title
    {
        get => _title;
        set
        {
            var title = value ?? string.Empty;
            if (string.Equals(_title, title, StringComparison.Ordinal))
            {
                return;
            }

            _title = title;
            OnPropertyChanged();
            _changed();
        }
    }

    public string StartTime
    {
        get => _startTime;
        set
        {
            if (SetProperty(ref _startTime, value ?? string.Empty))
            {
                _changed();
            }
        }
    }

    public string EndTime
    {
        get => _endTime;
        set
        {
            if (SetProperty(ref _endTime, value ?? string.Empty))
            {
                _changed();
            }
        }
    }

    public bool IsDone
    {
        get => _isDone;
        set
        {
            if (SetProperty(ref _isDone, value))
            {
                NotifyCompletion();
                _changed();
            }
        }
    }

    public bool IsPinned
    {
        get => _isPinned;
        set
        {
            if (SetProperty(ref _isPinned, value))
            {
                OnPropertyChanged(nameof(PinLabel));
                _changed();
            }
        }
    }

    public string PinLabel => _isPinned ? "取消置顶" : "置顶";

    /// <summary>列表默认展示文本；只有进入编辑态才展开输入字段。</summary>
    public bool IsEditing
    {
        get => _isEditing;
        set => SetProperty(ref _isEditing, value);
    }

    public bool HasSubtasks => Subtasks.Count > 0;

    public string ProgressText
    {
        get
        {
            var done = Subtasks.Count(item => item.IsDone);
            return $"{done}/{Subtasks.Count}";
        }
    }

    public bool IsCompleted
    {
        get => Subtasks.Count == 0 ? _isDone : Subtasks.All(item => item.IsDone);
        set
        {
            if (Subtasks.Count == 0)
            {
                IsDone = value;
                return;
            }

            _suppressChildChange = true;
            foreach (var subtask in Subtasks)
            {
                subtask.IsDone = value;
            }

            _suppressChildChange = false;
            NotifyCompletion();
            _changed();
        }
    }

    public void AddSubtask()
    {
        var subtask = new SubtaskItem("新子任务", false);
        subtask.PropertyChanged += Subtask_PropertyChanged;
        Subtasks.Add(subtask);
        OnPropertyChanged(nameof(HasSubtasks));
        NotifyCompletion();
        _changed();
    }

    public void RemoveSubtask(SubtaskItem subtask)
    {
        if (!Subtasks.Remove(subtask))
        {
            return;
        }

        subtask.PropertyChanged -= Subtask_PropertyChanged;
        OnPropertyChanged(nameof(HasSubtasks));
        NotifyCompletion();
        _changed();
    }

    private void Subtask_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_suppressChildChange)
        {
            return;
        }

        if (e.PropertyName == nameof(SubtaskItem.IsDone))
        {
            NotifyCompletion();
        }

        _changed();
    }

    private void NotifyCompletion()
    {
        OnPropertyChanged(nameof(IsCompleted));
        OnPropertyChanged(nameof(ProgressText));
    }
}

public sealed class TodoData
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public long CreatedAt { get; set; }
    public string StartTime { get; set; } = string.Empty;
    public string EndTime { get; set; } = string.Empty;
    public bool IsDone { get; set; }
    public bool IsPinned { get; set; }
    public List<SubtaskData> Subtasks { get; set; } = new();
}

public sealed class SubtaskData
{
    public string Title { get; set; } = string.Empty;
    public bool IsDone { get; set; }
}
