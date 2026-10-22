using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using UserControl = System.Windows.Controls.UserControl;

namespace NineMRoom.Client.App;

public partial class ChatPanel : UserControl
{
    private MainViewModel? _viewModel;

    public ChatPanel()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Loaded += (_, _) => Attach();
        Unloaded += (_, _) => Detach();
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e) => Attach();

    private void Attach()
    {
        var next = DataContext as MainViewModel;
        if (ReferenceEquals(next, _viewModel))
        {
            return;
        }

        Detach();
        _viewModel = next;
        if (_viewModel is not null)
        {
            _viewModel.ChatMessages.CollectionChanged += ChatMessages_CollectionChanged;
        }
    }

    private void Detach()
    {
        if (_viewModel is not null)
        {
            _viewModel.ChatMessages.CollectionChanged -= ChatMessages_CollectionChanged;
            _viewModel = null;
        }
    }

    private void ChatMessages_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        Dispatcher.BeginInvoke(
            new Action(() => ChatScroll.ScrollToEnd()),
            DispatcherPriority.Background);
    }

    private async void Send_Click(object sender, RoutedEventArgs e) => await SendAsync();

    private async void ChatBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            await SendAsync();
        }
    }

    private async Task SendAsync()
    {
        if (_viewModel is null)
        {
            return;
        }

        await _viewModel.SendChatAsync();
        ChatBox.Focus();
    }
}
