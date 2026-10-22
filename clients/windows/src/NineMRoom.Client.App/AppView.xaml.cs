using System.Windows;
using Button = System.Windows.Controls.Button;
using UserControl = System.Windows.Controls.UserControl;

namespace NineMRoom.Client.App;

public partial class AppView : UserControl
{
    public AppView()
    {
        InitializeComponent();
    }

    private MainViewModel? ViewModel => DataContext as MainViewModel;

    private void AppTile_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: AppRuleItem item } && ViewModel is not null)
        {
            ViewModel.OpenAppEditor(item);
        }
    }

    private void Sort_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string mode } && ViewModel is not null)
        {
            ViewModel.AppSortMode = mode;
        }
    }

    private void ClearSearch_Click(object sender, RoutedEventArgs e) => ViewModel?.ClearAppSearch();

    private void SearchBox_Click(object sender, System.Windows.Input.MouseButtonEventArgs e) =>
        AppSearchBox.Focus();

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
