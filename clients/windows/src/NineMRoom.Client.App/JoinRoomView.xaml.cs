using System.Windows;
using UserControl = System.Windows.Controls.UserControl;

namespace NineMRoom.Client.App;

public partial class JoinRoomView : UserControl
{
    public JoinRoomView()
    {
        InitializeComponent();
    }

    private MainViewModel? ViewModel => DataContext as MainViewModel;

    private void Cancel_Click(object sender, RoutedEventArgs e) => ViewModel?.CloseJoinDialog();

    private async void Join_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel is not null)
        {
            await ViewModel.ConfirmJoinAsync();
        }
    }
}
