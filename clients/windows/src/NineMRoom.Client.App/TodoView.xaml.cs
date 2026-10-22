using System.Windows;
using System.Windows.Input;
using Button = System.Windows.Controls.Button;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using TextBox = System.Windows.Controls.TextBox;
using UserControl = System.Windows.Controls.UserControl;

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

    private void Pin_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: TodoItem item })
        {
            item.IsPinned = !item.IsPinned;
        }
    }

    private void RemoveTodo_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: TodoItem item })
        {
            ViewModel?.RemoveTodo(item);
        }
    }

    private void AddSubtask_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: TodoItem item })
        {
            ViewModel?.AddSubtask(item);
        }
    }

    private void RemoveSubtask_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: SubtaskItem subtask })
        {
            ViewModel?.RemoveSubtask(subtask);
        }
    }
}
