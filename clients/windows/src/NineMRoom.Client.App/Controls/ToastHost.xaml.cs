using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using MaterialDesignThemes.Wpf;
using Border = System.Windows.Controls.Border;
using Button = System.Windows.Controls.Button;
using Grid = System.Windows.Controls.Grid;
using Point = System.Windows.Point;
using Brush = System.Windows.Media.Brush;
using HAlign = System.Windows.HorizontalAlignment;
using VAlign = System.Windows.VerticalAlignment;
using Panel = System.Windows.Controls.Panel;
using UserControl = System.Windows.Controls.UserControl;

namespace NineMRoom.Client.App.Controls;

public enum ToastKind
{
    Info,
    Success,
    Warning,
    Error
}

public sealed record ToastRequest(string Title, string Message, ToastKind Kind = ToastKind.Info, int DurationMs = 5000);

/// <summary>
/// 窗口右下角的应用内气泡通知：从下方滑入，顶部进度条随超时缩短，超时或关闭后向下滑出。
/// </summary>
public partial class ToastHost : UserControl
{
    private const int MaxVisible = 3;

    private readonly Dictionary<Border, DispatcherTimer> _timers = new();
    private readonly HashSet<Border> _closing = new();

    public ToastHost()
    {
        InitializeComponent();
    }

    public void Show(ToastRequest request)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(new Action(() => Show(request)));
            return;
        }

        var key = request.Title + "\n" + request.Message;
        foreach (var existing in Stack.Children.OfType<Border>())
        {
            if (existing.Tag as string == key && !_closing.Contains(existing))
            {
                return;
            }
        }

        while (Stack.Children.Count >= MaxVisible)
        {
            if (Stack.Children[0] is Border oldest)
            {
                Dismiss(oldest, immediate: true);
            }
            else
            {
                Stack.Children.RemoveAt(0);
            }
        }

        var card = CreateCard(request);
        card.Tag = key;
        Stack.Children.Add(card);
        AnimateIn(card);

        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(Math.Max(1200, request.DurationMs)) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            Dismiss(card);
        };
        _timers[card] = timer;
        timer.Start();
    }

    private Border CreateCard(ToastRequest request)
    {
        var card = new Border
        {
            Width = 360,
            Margin = new Thickness(0, 8, 0, 0),
            CornerRadius = new CornerRadius(12),
            BorderThickness = new Thickness(1),
            ClipToBounds = true,
            Effect = new DropShadowEffect
            {
                BlurRadius = 18,
                ShadowDepth = 2,
                Opacity = 0.35,
                Color = Colors.Black
            }
        };
        card.SetResourceReference(Border.BackgroundProperty, "BrushSurfaceContainerHigh");
        card.SetResourceReference(Border.BorderBrushProperty, "BrushOutlineVariant");

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(3) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var progressTrack = new Grid();
        progressTrack.SetResourceReference(Panel.BackgroundProperty, "BrushOutlineVariant");
        var progress = new Border { HorizontalAlignment = HAlign.Stretch };
        progress.SetResourceReference(Border.BackgroundProperty, "BrushPrimary");
        progress.RenderTransformOrigin = new Point(0, 0.5);
        var progressScale = new ScaleTransform(1, 1);
        progress.RenderTransform = progressScale;
        progressTrack.Children.Add(progress);
        Grid.SetRow(progressTrack, 0);
        root.Children.Add(progressTrack);

        var content = new Grid { Margin = new Thickness(14, 12, 10, 12) };
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var icon = new PackIcon
        {
            Kind = request.Kind switch
            {
                ToastKind.Success => PackIconKind.CheckCircleOutline,
                ToastKind.Warning => PackIconKind.AlertCircleOutline,
                ToastKind.Error => PackIconKind.CloseCircleOutline,
                _ => PackIconKind.InformationOutline
            },
            Width = 20,
            Height = 20,
            VerticalAlignment = VAlign.Top,
            Margin = new Thickness(0, 2, 10, 0)
        };
        icon.SetResourceReference(ForegroundProperty, request.Kind == ToastKind.Error ? "BrushError" : "BrushPrimary");
        content.Children.Add(icon);

        var text = new StackPanel { VerticalAlignment = VAlign.Center };
        var title = new TextBlock { Text = request.Title, TextWrapping = TextWrapping.Wrap };
        if (TryFindResource("ItemTitle") is Style titleStyle)
        {
            title.Style = titleStyle;
        }

        var message = new TextBlock
        {
            Text = request.Message,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 2, 0, 0)
        };
        if (TryFindResource("ItemSupporting") is Style bodyStyle)
        {
            message.Style = bodyStyle;
        }

        text.Children.Add(title);
        text.Children.Add(message);
        Grid.SetColumn(text, 1);
        content.Children.Add(text);

        var closeIcon = new PackIcon
        {
            Kind = PackIconKind.Close,
            Width = 14,
            Height = 14,
            Foreground = (Brush)TryFindResource("BrushOnSurfaceVariant")
        };
        var close = new M3Button
        {
            Style = TryFindResource("M3IconButton") as Style,
            Width = 24,
            Height = 24,
            MinWidth = 0,
            MinHeight = 0,
            Padding = new Thickness(0),
            Content = closeIcon,
            VerticalAlignment = VAlign.Top,
            HorizontalAlignment = HAlign.Right,
            Margin = new Thickness(4, -2, 0, 0)
        };
        close.Click += (_, _) => Dismiss(card);
        Grid.SetColumn(close, 2);
        content.Children.Add(close);

        Grid.SetRow(content, 1);
        root.Children.Add(content);
        card.Child = root;

        card.Opacity = 0;
        card.RenderTransformOrigin = new Point(0.5, 1);
        card.RenderTransform = new TranslateTransform(0, 28);

        progressScale.BeginAnimation(
            ScaleTransform.ScaleXProperty,
            new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(Math.Max(1200, request.DurationMs)))
            {
                FillBehavior = FillBehavior.HoldEnd
            });

        return card;
    }

    private static void AnimateIn(Border card)
    {
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        card.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180))
        {
            EasingFunction = ease,
            FillBehavior = FillBehavior.HoldEnd
        });

        if (card.RenderTransform is TranslateTransform transform)
        {
            transform.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(28, 0, TimeSpan.FromMilliseconds(220))
            {
                EasingFunction = ease,
                FillBehavior = FillBehavior.HoldEnd
            });
        }
    }

    private void Dismiss(Border card, bool immediate = false)
    {
        if (!_closing.Add(card))
        {
            return;
        }

        if (_timers.Remove(card, out var timer))
        {
            timer.Stop();
        }

        if (immediate)
        {
            Stack.Children.Remove(card);
            _closing.Remove(card);
            return;
        }

        var ease = new CubicEase { EasingMode = EasingMode.EaseIn };
        var fade = new DoubleAnimation(0, TimeSpan.FromMilliseconds(180))
        {
            EasingFunction = ease,
            FillBehavior = FillBehavior.HoldEnd
        };
        fade.Completed += (_, _) =>
        {
            Stack.Children.Remove(card);
            _closing.Remove(card);
        };
        card.BeginAnimation(OpacityProperty, fade);

        if (card.RenderTransform is TranslateTransform transform)
        {
            transform.BeginAnimation(
                TranslateTransform.YProperty,
                new DoubleAnimation(32, TimeSpan.FromMilliseconds(200))
                {
                    EasingFunction = ease,
                    FillBehavior = FillBehavior.HoldEnd
                });
        }
    }
}
