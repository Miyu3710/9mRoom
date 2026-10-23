using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using ScrollBar = System.Windows.Controls.Primitives.ScrollBar;

namespace NineMRoom.Client.App;

/// <summary>
/// 滚动条自动隐藏：滚动或悬停时淡入，停止约 1.1 秒后淡出。
/// 只影响滚动条本身，不影响内容与布局。
/// </summary>
public static class ScrollFade
{
    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
        "IsEnabled",
        typeof(bool),
        typeof(ScrollFade),
        new PropertyMetadata(false, OnIsEnabledChanged));

    private static readonly ConditionalWeakTable<ScrollViewer, Host> Hosts = new();

    public static void SetIsEnabled(DependencyObject element, bool value) => element.SetValue(IsEnabledProperty, value);

    public static bool GetIsEnabled(DependencyObject element) => (bool)element.GetValue(IsEnabledProperty);

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ScrollViewer viewer || e.NewValue is not true)
        {
            return;
        }

        Hosts.GetValue(viewer, value => new Host(value));
    }

    private sealed class Host
    {
        private readonly ScrollViewer _viewer;
        private readonly List<ScrollBar> _bars = new();
        private readonly DispatcherTimer _hideTimer = new() { Interval = TimeSpan.FromMilliseconds(1100) };
        private bool _attached;

        public Host(ScrollViewer viewer)
        {
            _viewer = viewer;
            _hideTimer.Tick += (_, _) => FadeOut();
            viewer.Loaded += (_, _) => Attach();
            viewer.Unloaded += (_, _) => Detach();

            if (viewer.IsLoaded)
            {
                Attach();
            }
        }

        private void Attach()
        {
            if (!_attached)
            {
                _attached = true;
                _viewer.ScrollChanged += Viewer_ScrollChanged;
            }

            CollectBars();
            SetOpacity(0);
        }

        private void Detach()
        {
            _hideTimer.Stop();
            _viewer.ScrollChanged -= Viewer_ScrollChanged;
            _attached = false;

            foreach (var bar in _bars)
            {
                bar.MouseEnter -= Bar_MouseEnter;
                bar.MouseLeave -= Bar_MouseLeave;
            }

            _bars.Clear();
        }

        private void CollectBars()
        {
            if (_viewer.Template is null)
            {
                return;
            }

            _viewer.ApplyTemplate();

            if (_viewer.Template.FindName("PART_VerticalScrollBar", _viewer) is ScrollBar vertical &&
                !_bars.Contains(vertical))
            {
                Add(vertical);
            }

            if (_viewer.Template.FindName("PART_HorizontalScrollBar", _viewer) is ScrollBar horizontal &&
                !_bars.Contains(horizontal))
            {
                Add(horizontal);
            }

            void Add(ScrollBar bar)
            {
                _bars.Add(bar);
                bar.MouseEnter += Bar_MouseEnter;
                bar.MouseLeave += Bar_MouseLeave;
            }
        }

        private void Viewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (Math.Abs(e.VerticalChange) < 0.01 && Math.Abs(e.HorizontalChange) < 0.01 &&
                Math.Abs(e.ExtentHeightChange) < 0.01 && Math.Abs(e.ViewportHeightChange) < 0.01)
            {
                return;
            }

            Show();
        }

        private void Bar_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            _hideTimer.Stop();
            Show(keepVisible: true);
        }

        private void Bar_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e) => ScheduleHide();

        private void Show(bool keepVisible = false)
        {
            _hideTimer.Stop();
            AnimateTo(1, TimeSpan.FromMilliseconds(120));

            if (!keepVisible)
            {
                ScheduleHide();
            }
        }

        private void ScheduleHide()
        {
            _hideTimer.Stop();
            _hideTimer.Start();
        }

        private void FadeOut()
        {
            if (_bars.Any(bar => bar.IsMouseOver || bar.IsMouseCaptureWithin))
            {
                ScheduleHide();
                return;
            }

            AnimateTo(0, TimeSpan.FromMilliseconds(260));
        }

        private void SetOpacity(double value)
        {
            foreach (var bar in _bars)
            {
                bar.Opacity = value;
            }
        }

        private void AnimateTo(double target, TimeSpan duration)
        {
            var animation = new DoubleAnimation(target, duration)
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
                FillBehavior = FillBehavior.HoldEnd
            };

            foreach (var bar in _bars)
            {
                bar.BeginAnimation(UIElement.OpacityProperty, animation);
            }
        }
    }
}
