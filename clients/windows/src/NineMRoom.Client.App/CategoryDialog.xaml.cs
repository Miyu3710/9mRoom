using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using Brush = System.Windows.Media.Brush;

using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using Point = System.Windows.Point;

namespace NineMRoom.Client.App;

public partial class CategoryDialog : Window
{
    private AppCategoryItem? _dragCategory;
    private UIElement? _dragSource;
    private Point _dragStart;
    private bool _dragging;
    private int _insertIndex;
    private Border? _ghost;
    private Border? _insertionLine;

    public CategoryDialog()
    {
        InitializeComponent();
    }

    private MainViewModel? ViewModel => DataContext as MainViewModel;

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        ViewModel?.AddAppCategory(NewNameBox.Text);
        NewNameBox.Clear();
        NewTemplateBox.Clear();
    }

    private void Remove_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: AppCategoryItem item })
        {
            ViewModel?.RemoveAppCategory(item);
        }
    }

    private void Handle_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: AppCategoryItem item } handle)
        {
            return;
        }

        _dragCategory = item;
        _dragSource = handle;
        _dragStart = e.GetPosition(null);
        _dragging = false;
        _insertIndex = ViewModel?.AppCategories.IndexOf(item) ?? 0;
        handle.CaptureMouse();
        e.Handled = true;
    }

    private void Handle_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (_dragCategory is null || _dragSource is null || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var position = e.GetPosition(null);
        if (!_dragging &&
            Math.Abs(position.X - _dragStart.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(position.Y - _dragStart.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        if (!_dragging)
        {
            StartDrag();
        }

        UpdateDrag(e.GetPosition(DragLayer));
        e.Handled = true;
    }

    private void Handle_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_dragging && _dragCategory is not null)
        {
            ViewModel?.MoveAppCategoryToIndex(_dragCategory, _insertIndex);
        }

        FinishDrag();
        e.Handled = true;
    }

    private void StartDrag()
    {
        if (_dragCategory is null)
        {
            return;
        }

        _dragging = true;
        _dragCategory.IsDragging = true;
        Cursor = System.Windows.Input.Cursors.SizeAll;

        var origin = CategoryItems.TranslatePoint(new Point(0, 0), DragLayer);
        var ghostText = new TextBlock
        {
            Text = _dragCategory.Name,
            FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)FindResource("BrushOnSurface")
        };

        _ghost = new Border
        {
            Background = (Brush)FindResource("BrushSurfaceContainerHigh"),
            BorderBrush = (Brush)FindResource("BrushPrimary"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(12, 8, 12, 8),
            Width = Math.Max(160, CategoryItems.ActualWidth),
            Opacity = 0.96,
            IsHitTestVisible = false,
            Effect = new DropShadowEffect { BlurRadius = 18, ShadowDepth = 4, Opacity = 0.45 },
            Child = ghostText
        };

        _insertionLine = new Border
        {
            Height = 2,
            Width = Math.Max(0, CategoryItems.ActualWidth),
            Background = (Brush)FindResource("BrushPrimary"),
            CornerRadius = new CornerRadius(1),
            IsHitTestVisible = false
        };

        Canvas.SetLeft(_ghost, origin.X);
        Canvas.SetLeft(_insertionLine, origin.X);
        DragLayer.Children.Add(_ghost);
        DragLayer.Children.Add(_insertionLine);
    }

    private void UpdateDrag(Point pointer)
    {
        if (_ghost is not null)
        {
            var top = Math.Clamp(pointer.Y - 18, 0, Math.Max(0, DragLayer.ActualHeight - 40));
            Canvas.SetTop(_ghost, top);
        }

        var pointerInItems = Mouse.GetPosition(CategoryItems);
        var (index, y) = FindDropTarget(pointerInItems.Y);
        _insertIndex = index;

        if (_insertionLine is not null)
        {
            var origin = CategoryItems.TranslatePoint(new Point(0, 0), DragLayer);
            _insertionLine.Width = Math.Max(0, CategoryItems.ActualWidth);
            Canvas.SetTop(_insertionLine, Math.Max(0, origin.Y + y));
        }
    }

    private (int Index, double Y) FindDropTarget(double pointerY)
    {
        var count = CategoryItems.Items.Count;
        var lastBottom = 0.0;

        for (var i = 0; i < count; i++)
        {
            if (CategoryItems.ItemContainerGenerator.ContainerFromIndex(i) is not FrameworkElement container)
            {
                continue;
            }

            var top = container.TranslatePoint(new Point(0, 0), CategoryItems).Y;
            var bottom = top + container.ActualHeight;
            lastBottom = bottom;

            if (pointerY < top + container.ActualHeight / 2)
            {
                return (i, top - 4);
            }
        }

        return (count, lastBottom + 2);
    }

    private void FinishDrag()
    {
        if (_dragCategory is not null)
        {
            _dragCategory.IsDragging = false;
        }

        if (_ghost is not null)
        {
            DragLayer.Children.Remove(_ghost);
            _ghost = null;
        }

        if (_insertionLine is not null)
        {
            DragLayer.Children.Remove(_insertionLine);
            _insertionLine = null;
        }

        _dragSource?.ReleaseMouseCapture();
        _dragSource = null;
        _dragCategory = null;
        _dragging = false;
        Cursor = System.Windows.Input.Cursors.Arrow;
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
