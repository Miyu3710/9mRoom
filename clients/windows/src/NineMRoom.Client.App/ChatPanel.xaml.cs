using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
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
        SendButton.RenderTransformOrigin = new System.Windows.Point(0.5, 0.5);
        SendButton.RenderTransform = new ScaleTransform(1, 1);
        DataContextChanged += OnDataContextChanged;
        Loaded += (_, _) =>
        {
            Attach();
            ScrollToEnd();
        };
        Unloaded += (_, _) => Detach();
        IsVisibleChanged += (_, _) =>
        {
            if (IsVisible)
            {
                ScrollToEnd();
            }
        };
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
            new Action(() =>
            {
                ScrollToEnd();
                AnimateNewMessages(e);
            }),
            DispatcherPriority.Background);
    }

    private void ScrollToEnd()
    {
        if (ChatScroll is null)
        {
            return;
        }

        ChatScroll.ScrollToEnd();
    }

    private void AnimateNewMessages(NotifyCollectionChangedEventArgs e)
    {
        if (e.Action != NotifyCollectionChangedAction.Add || e.NewItems is null)
        {
            return;
        }

        foreach (var item in e.NewItems)
        {
            if (ChatItems.ItemContainerGenerator.ContainerFromItem(item) is not FrameworkElement container)
            {
                continue;
            }

            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            var translate = new TranslateTransform(0, 16);
            container.RenderTransform = translate;
            container.Opacity = 0;
            container.BeginAnimation(
                OpacityProperty,
                new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(190))
                {
                    EasingFunction = ease,
                    FillBehavior = FillBehavior.HoldEnd
                });
            translate.BeginAnimation(
                TranslateTransform.YProperty,
                new DoubleAnimation(16, 0, TimeSpan.FromMilliseconds(240))
                {
                    EasingFunction = ease,
                    FillBehavior = FillBehavior.HoldEnd
                });
        }
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

        if (SendButton.RenderTransform is ScaleTransform scale)
        {
            scale.BeginAnimation(
                ScaleTransform.ScaleXProperty,
                new DoubleAnimation(0.9, 1, TimeSpan.FromMilliseconds(220))
                {
                    EasingFunction = new BackEase { Amplitude = 0.4, EasingMode = EasingMode.EaseOut },
                    FillBehavior = FillBehavior.HoldEnd
                });
            scale.BeginAnimation(
                ScaleTransform.ScaleYProperty,
                new DoubleAnimation(0.9, 1, TimeSpan.FromMilliseconds(220))
                {
                    EasingFunction = new BackEase { Amplitude = 0.4, EasingMode = EasingMode.EaseOut },
                    FillBehavior = FillBehavior.HoldEnd
                });
        }
    }
}
