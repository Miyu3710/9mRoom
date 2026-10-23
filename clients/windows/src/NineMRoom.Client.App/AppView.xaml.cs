using System.Windows;
using MouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;
using RadioButton = System.Windows.Controls.RadioButton;
using SelectionChangedEventArgs = System.Windows.Controls.SelectionChangedEventArgs;
using UserControl = System.Windows.Controls.UserControl;
using Window = System.Windows.Window;

namespace NineMRoom.Client.App;

public partial class AppView : UserControl
{
    public AppView()
    {
        InitializeComponent();
    }

    private MainViewModel? ViewModel => DataContext as MainViewModel;

    private void AppList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (AppList.SelectedItem is AppRuleItem item && ViewModel is not null)
        {
            ViewModel.OpenAppEditor(item);
        }
    }

    private void AppList_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (AppList.SelectedItem is not AppRuleItem item || ViewModel is null)
        {
            return;
        }

        var source = e.OriginalSource as DependencyObject;
        var container = source is null ? null : System.Windows.Controls.ItemsControl.ContainerFromElement(AppList, source) as System.Windows.Controls.ListBoxItem;
        if (container?.DataContext is AppRuleItem clicked && ReferenceEquals(clicked, item))
        {
            ViewModel.OpenAppEditor(item);
        }
    }

    private void Sort_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton { Tag: string mode } && ViewModel is not null)
        {
            ViewModel.AppSortMode = mode;
        }
    }

    private void ClearSearch_Click(object sender, RoutedEventArgs e) => ViewModel?.ClearAppSearch();

    private void CategoryManage_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel is null)
        {
            return;
        }

        var dialog = new CategoryDialog
        {
            Owner = Window.GetWindow(this),
            DataContext = ViewModel
        };
        dialog.ShowDialog();
    }
}
