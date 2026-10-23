using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Button = System.Windows.Controls.Button;
using Canvas = System.Windows.Controls.Canvas;
using Control = System.Windows.Controls.Control;
using Panel = System.Windows.Controls.Panel;
using TextBox = System.Windows.Controls.TextBox;
using TextBlock = System.Windows.Controls.TextBlock;
using VisualTreeHelper = System.Windows.Media.VisualTreeHelper;

namespace NineMRoom.Client.App.Controls;

/// <summary>
/// Outlined text field 的项目附加属性：标签、支持文本、前导图标、缺口底色，
/// 以及浮动标签动画与清除按钮接线。标签位置在“内容区居中”和“缺口”之间动画过渡。
/// </summary>
public static class M3Field
{
    public static readonly DependencyProperty LabelProperty = DependencyProperty.RegisterAttached(
        "Label",
        typeof(string),
        typeof(M3Field),
        new FrameworkPropertyMetadata(string.Empty));

    public static readonly DependencyProperty HelperTextProperty = DependencyProperty.RegisterAttached(
        "HelperText",
        typeof(string),
        typeof(M3Field),
        new FrameworkPropertyMetadata(string.Empty));

    public static readonly DependencyProperty LeadingIconProperty = DependencyProperty.RegisterAttached(
        "LeadingIcon",
        typeof(MaterialDesignThemes.Wpf.PackIconKind),
        typeof(M3Field),
        new FrameworkPropertyMetadata(MaterialDesignThemes.Wpf.PackIconKind.Search));

    public static readonly DependencyProperty ShowLeadingIconProperty = DependencyProperty.RegisterAttached(
        "ShowLeadingIcon",
        typeof(bool),
        typeof(M3Field),
        new FrameworkPropertyMetadata(false));

    public static readonly DependencyProperty NotchBrushProperty = DependencyProperty.RegisterAttached(
        "NotchBrush",
        typeof(Brush),
        typeof(M3Field),
        new FrameworkPropertyMetadata(null));

    public static readonly DependencyProperty AutoNotchProperty = DependencyProperty.RegisterAttached(
        "AutoNotch",
        typeof(bool),
        typeof(M3Field),
        new FrameworkPropertyMetadata(false, OnAutoNotchChanged));

    private static readonly ConditionalWeakTable<Control, FieldChrome> Hooks = new();

    public static void SetLabel(DependencyObject element, string value) => element.SetValue(LabelProperty, value);

    public static string GetLabel(DependencyObject element) => (string)element.GetValue(LabelProperty);

    public static void SetHelperText(DependencyObject element, string value) => element.SetValue(HelperTextProperty, value);

    public static string GetHelperText(DependencyObject element) => (string)element.GetValue(HelperTextProperty);

    public static void SetLeadingIcon(DependencyObject element, MaterialDesignThemes.Wpf.PackIconKind value) =>
        element.SetValue(LeadingIconProperty, value);

    public static MaterialDesignThemes.Wpf.PackIconKind GetLeadingIcon(DependencyObject element) =>
        (MaterialDesignThemes.Wpf.PackIconKind)element.GetValue(LeadingIconProperty);

    public static void SetShowLeadingIcon(DependencyObject element, bool value) => element.SetValue(ShowLeadingIconProperty, value);

    public static bool GetShowLeadingIcon(DependencyObject element) => (bool)element.GetValue(ShowLeadingIconProperty);

    public static void SetNotchBrush(DependencyObject element, Brush? value) => element.SetValue(NotchBrushProperty, value);

    public static Brush? GetNotchBrush(DependencyObject element) => (Brush?)element.GetValue(NotchBrushProperty);

    public static void SetAutoNotch(DependencyObject element, bool value) => element.SetValue(AutoNotchProperty, value);

    public static bool GetAutoNotch(DependencyObject element) => (bool)element.GetValue(AutoNotchProperty);

    private static void OnAutoNotchChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is Control control && e.NewValue is true)
        {
            Hooks.GetValue(control, value => new FieldChrome(value));
        }
    }

    private sealed class FieldChrome
    {
        private const double InsideTop = 29;
        private const double FloatedTop = 0;
        private static readonly TimeSpan FloatDuration = TimeSpan.FromMilliseconds(180);

        private readonly Control _control;
        private readonly TextBox? _textBox;
        private TextBlock? _label;
        private Button? _clearButton;
        private bool _initialized;

        public FieldChrome(Control control)
        {
            _control = control;
            _textBox = control as TextBox;
            _control.Loaded += OnLoaded;
            _control.Unloaded += OnUnloaded;

            if (_textBox is not null)
            {
                _textBox.TextChanged += (_, _) => UpdateLabel(animate: true);
                _textBox.IsKeyboardFocusWithinChanged += (_, _) => UpdateLabel(animate: true);
                DependencyPropertyDescriptor
                    .FromProperty(TextBox.IsReadOnlyProperty, typeof(TextBox))
                    .AddValueChanged(_textBox, (_, _) => UpdateLabel(animate: true));
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (GetNotchBrush(_control) is null)
            {
                var brush = FindAncestorBackground(_control);
                if (brush is not null)
                {
                    SetNotchBrush(_control, brush);
                }
            }

            AttachClearButton();
            _label = _control.Template?.FindName("FieldLabel", _control) as TextBlock;
            _initialized = false;
            UpdateLabel(animate: false);
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            if (_clearButton is not null)
            {
                _clearButton.Click -= Clear_Click;
                _clearButton = null;
            }
        }

        private void AttachClearButton()
        {
            if (_control.Template?.FindName("PART_ClearButton", _control) is not Button button ||
                ReferenceEquals(button, _clearButton))
            {
                return;
            }

            if (_clearButton is not null)
            {
                _clearButton.Click -= Clear_Click;
            }

            _clearButton = button;
            _clearButton.Click += Clear_Click;
        }

        private void UpdateClearButton()
        {
            if (_clearButton is null || _textBox is null)
            {
                return;
            }

            _clearButton.Visibility = !_textBox.IsReadOnly &&
                                      _textBox.IsEnabled &&
                                      !string.IsNullOrEmpty(_textBox.Text)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void UpdateLabel(bool animate)
        {
            if (_label is null || _textBox is null)
            {
                return;
            }

            UpdateClearButton();

            var floating = _textBox.IsKeyboardFocusWithin ||
                           !string.IsNullOrEmpty(_textBox.Text) ||
                           _textBox.IsReadOnly;
            var leading = GetShowLeadingIcon(_textBox);
            var small = ResourceDouble("TypeBodySmallSize", 12);
            var large = ResourceDouble("TypeBodyLargeSize", 16);

            var top = floating ? FloatedTop : InsideTop;
            var left = floating ? 12d : leading ? 46d : 16d;
            var fontSize = floating ? small : large;

            _label.Padding = floating ? new Thickness(4, 0, 4, 0) : new Thickness(0);
            _label.Background = floating
                ? GetNotchBrush(_textBox) ?? Brushes.Transparent
                : Brushes.Transparent;
            _label.Foreground = ResolveForeground(floating);

            if (!animate || !_initialized)
            {
                Canvas.SetTop(_label, top);
                Canvas.SetLeft(_label, left);
                _label.FontSize = fontSize;
            }
            else
            {
                Animate(Canvas.TopProperty, top);
                Animate(Canvas.LeftProperty, left);
                Animate(TextBlock.FontSizeProperty, fontSize);
            }

            _initialized = true;
        }

        private Brush ResolveForeground(bool floating)
        {
            if (_textBox is not null && Validation.GetHasError(_textBox))
            {
                return _control.TryFindResource("BrushError") as Brush ?? Brushes.OrangeRed;
            }

            if (floating && _textBox is { IsReadOnly: false, IsKeyboardFocusWithin: true })
            {
                return _control.TryFindResource("BrushPrimary") as Brush ?? Brushes.DodgerBlue;
            }

            return _control.TryFindResource("BrushOnSurfaceVariant") as Brush ?? Brushes.Gray;
        }

        private void Animate(DependencyProperty property, double to)
        {
            if (_label is null)
            {
                return;
            }

            _label.BeginAnimation(
                property,
                new DoubleAnimation(to, FloatDuration)
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
                    FillBehavior = FillBehavior.HoldEnd
                });
        }

        private double ResourceDouble(string key, double fallback) =>
            _control.TryFindResource(key) is double value ? value : fallback;

        private void Clear_Click(object sender, RoutedEventArgs e)
        {
            if (_textBox is not null)
            {
                _textBox.Clear();
                _textBox.Focus();
            }

            e.Handled = true;
        }

        private static Brush? FindAncestorBackground(DependencyObject element)
        {
            var current = VisualTreeHelper.GetParent(element);
            while (current is not null)
            {
                Brush? background = current switch
                {
                    Border border => border.Background,
                    Panel panel => panel.Background,
                    Control control => control.Background,
                    _ => null
                };

                if (background is System.Windows.Media.SolidColorBrush { Color.A: > 0 } solid)
                {
                    return solid;
                }

                current = VisualTreeHelper.GetParent(current);
            }

            return null;
        }
    }
}
