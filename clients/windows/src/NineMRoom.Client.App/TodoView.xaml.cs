using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MenuItem = System.Windows.Controls.MenuItem;
using TextBox = System.Windows.Controls.TextBox;
using UserControl = System.Windows.Controls.UserControl;
using Window = System.Windows.Window;

namespace NineMRoom.Client.App;

public partial class TodoView : UserControl
{
    public TodoView()
    {
        InitializeComponent();
    }

    private MainViewModel? ViewModel => DataContext as MainViewModel;

    private void AddTodo_Click(object sender, RoutedEventArgs e) => AddFromBox();

    private void NewTodo_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            AddFromBox();
        }
    }

    private void AddFromBox()
    {
        ViewModel?.AddTodo(NewTodoBox.Text);
        NewTodoBox.Clear();
        NewTodoBox.Focus();
    }

    private void TodoMenu_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { ContextMenu: { } menu } element)
        {
            return;
        }

        menu.PlacementTarget = element;
        menu.Placement = PlacementMode.Bottom;
        menu.IsOpen = true;
    }

    private void EditTodo_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { Tag: TodoItem item })
        {
            item.IsEditing = true;
        }
    }

    private void DoneEdit_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: TodoItem item })
        {
            item.IsEditing = false;
        }
    }

    private void Pin_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { Tag: TodoItem item })
        {
            item.IsPinned = !item.IsPinned;
        }
    }

    private void RemoveTodo_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { Tag: TodoItem item })
        {
            ViewModel?.RemoveTodo(item);
        }
    }

    private void AddSubtask_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: TodoItem item })
        {
            ViewModel?.AddSubtask(item);
        }
    }

    private void RemoveSubtask_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: SubtaskItem subtask })
        {
            ViewModel?.RemoveSubtask(subtask);
        }
    }

    private void PickStartTime_Click(object sender, RoutedEventArgs e) => PickTime(sender, isStart: true);

    private void PickEndTime_Click(object sender, RoutedEventArgs e) => PickTime(sender, isStart: false);

    private void PickTime(object sender, bool isStart)
    {
        if (sender is not FrameworkElement { Tag: TodoItem item })
        {
            return;
        }

        var current = isStart ? item.StartTime : item.EndTime;
        var value = Controls.M3DateTimeDialog.Pick(Window.GetWindow(this), current);
        if (value is null)
        {
            return;
        }

        if (isStart)
        {
            item.StartTime = value;
        }
        else
        {
            item.EndTime = value;
        }
    }
}
