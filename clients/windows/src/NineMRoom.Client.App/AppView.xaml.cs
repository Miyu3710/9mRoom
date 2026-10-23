using System.Windows;
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
            // 清掉选中态，保证再次点击同一磁贴仍能触发 SelectionChanged 重新打开抽屉。
            Dispatcher.BeginInvoke(
                new Action(() =>
                {
                    if (ReferenceEquals(AppList.SelectedItem, item))
                    {
                        AppList.SelectedItem = null;
                    }
                }),
                System.Windows.Threading.DispatcherPriority.Background);
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
