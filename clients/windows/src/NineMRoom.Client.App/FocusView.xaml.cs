using System.Windows;
using Button = System.Windows.Controls.Button;
using UserControl = System.Windows.Controls.UserControl;

namespace NineMRoom.Client.App;

public partial class FocusView : UserControl
{
    public FocusView()
    {
        InitializeComponent();
    }

    private MainViewModel? ViewModel => DataContext as MainViewModel;

    private void TimerStart_Click(object sender, RoutedEventArgs e) => ViewModel?.StartTimer();

    private void TimerPause_Click(object sender, RoutedEventArgs e) => ViewModel?.PauseTimer();

    private void TimerReset_Click(object sender, RoutedEventArgs e) => ViewModel?.ResetTimer();

    private void TimerMinutes_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string minutes } && int.TryParse(minutes, out var value))
        {
            ViewModel?.SetTimerMinutes(value);
        }
    }
}
