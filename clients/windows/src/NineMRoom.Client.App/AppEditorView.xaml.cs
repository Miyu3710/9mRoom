using System.Windows;
using Button = System.Windows.Controls.Button;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using UserControl = System.Windows.Controls.UserControl;

namespace NineMRoom.Client.App;

public partial class AppEditorView : UserControl
{
    public AppEditorView()
    {
        InitializeComponent();
    }

    private MainViewModel? ViewModel => DataContext as MainViewModel;

    private void Close_Click(object sender, RoutedEventArgs e) => ViewModel?.CancelAppRuleDraft();

    private void Save_Click(object sender, RoutedEventArgs e) => ViewModel?.SaveAppRuleDraft();

    private void Cancel_Click(object sender, RoutedEventArgs e) => ViewModel?.CancelAppRuleDraft();

    private void Help_Click(object sender, RoutedEventArgs e) =>
        TemplateHelpPopup.IsOpen = !TemplateHelpPopup.IsOpen;

    private void CustomIcon_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "选择应用图标",
            Filter = "所有文件 (*.*)|*.*|图片 (*.png;*.jpg;*.jpeg;*.ico)|*.png;*.jpg;*.jpeg;*.ico|程序 (*.exe)|*.exe"
        };

        if (dialog.ShowDialog() == true)
        {
            ViewModel?.ApplyDraftIcon(dialog.FileName);
        }
    }

    private void ResetIcon_Click(object sender, RoutedEventArgs e) => ViewModel?.ResetDraftIcon();

    private void Category_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string category } && ViewModel?.AppDraft is { } draft)
        {
            draft.CategoryOverride = category;
        }
    }
}
