using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;
using TextCompositionEventArgs = System.Windows.Input.TextCompositionEventArgs;
using TextBox = System.Windows.Controls.TextBox;
using UserControl = System.Windows.Controls.UserControl;

namespace NineMRoom.Client.App;

public partial class FocusView : UserControl
{
    public FocusView()
    {
        InitializeComponent();
    }

    private MainViewModel? ViewModel => DataContext as MainViewModel;

    private void TimerToggle_Click(object sender, RoutedEventArgs e) => ViewModel?.ToggleTimer();

    private void TimerReset_Click(object sender, RoutedEventArgs e) => ViewModel?.ResetTimer();

    private void Minutes_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        foreach (var character in e.Text)
        {
            if (!char.IsDigit(character))
            {
                e.Handled = true;
                return;
            }
        }
    }

    private void Minutes_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is not TextBox box ||
            box.GetBindingExpression(TextBox.TextProperty) is not { } binding)
        {
            return;
        }

        binding.UpdateSource();
        if (binding.HasError || string.IsNullOrWhiteSpace(box.Text))
        {
            binding.UpdateTarget();
        }
    }
}
