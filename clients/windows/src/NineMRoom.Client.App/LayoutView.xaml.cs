using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using Brush = System.Windows.Media.Brush;
using Border = System.Windows.Controls.Border;
using Cursors = System.Windows.Input.Cursors;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using Point = System.Windows.Point;
using ScrollViewer = System.Windows.Controls.ScrollViewer;
using UserControl = System.Windows.Controls.UserControl;
using VisualTreeHelper = System.Windows.Media.VisualTreeHelper;

namespace NineMRoom.Client.App;

public partial class LayoutView : UserControl
{
    private Point _dragStart;
    private StatusBlockItem? _dragItem;
    private UIElement? _dragSource;
    private bool _dragging;
    private Border? _ghost;
    private ScrollViewer? _scrollViewer;
    private int _lastTargetIndex = -1;

    public LayoutView()
    {
        InitializeComponent();
        LayoutItems.PreviewMouseMove += DragHandle_MouseMove;
        LayoutItems.PreviewMouseLeftButtonUp += DragHandle_MouseLeftButtonUp;
    }

    private MainViewModel? ViewModel => DataContext as MainViewModel;

    private void AddBlock_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string type })
        {
            ViewModel?.AddLayoutBlock(type);
        }
    }

    private void AddCustom_Click(object sender, RoutedEventArgs e) => ViewModel?.AddCustomBlock();

    private void Help_Click(object sender, RoutedEventArgs e) => HelpPopup.IsOpen = !HelpPopup.IsOpen;

    private void Remove_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: StatusBlockItem item })
        {
            ViewModel?.RemoveLayoutBlock(item);
        }
    }

    private void DragHandle_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStart = e.GetPosition(null);
        _dragItem = (sender as FrameworkElement)?.DataContext as StatusBlockItem;
        _dragging = false;
        _lastTargetIndex = _dragItem is null ? -1 : ViewModel?.LayoutBlocks.IndexOf(_dragItem) ?? -1;
        _scrollViewer = FindAncestor<ScrollViewer>(this);

        _dragSource = LayoutItems;
        LayoutItems.CaptureMouse();
        e.Handled = true;
    }

    private void DragHandle_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _dragItem is null)
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

        UpdateDrag(e);
        AutoScroll(e);
        e.Handled = true;
    }

    private void DragHandle_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        FinishDrag();
    }

    private void StartDrag()
    {
        if (_dragItem is null)
        {
            return;
        }

        _dragging = true;
        _dragItem.IsDragging = true;
        Cursor = Cursors.SizeAll;

        var label = new TextBlock
        {
            Text = _dragItem.Label,
            FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)FindResource("BrushOnSurface")
        };

        var template = new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(_dragItem.Template) ? "（空模板）" : _dragItem.Template,
            FontSize = 12,
            Foreground = (Brush)FindResource("BrushOnSurfaceVariant"),
            TextTrimming = TextTrimming.CharacterEllipsis,
            Margin = new Thickness(0, 2, 0, 0)
        };

        _ghost = new Border
        {
            Background = (Brush)FindResource("BrushSurfaceContainerHigh"),
            BorderBrush = (Brush)FindResource("BrushPrimary"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(12, 8, 12, 8),
            Width = Math.Max(260, LayoutItems.ActualWidth - 16),
            Opacity = 0.96,
            IsHitTestVisible = false,
            Effect = new DropShadowEffect { BlurRadius = 18, ShadowDepth = 4, Opacity = 0.45 },
            Child = new StackPanel
            {
                Children = { label, template }
            }
        };

        Canvas.SetLeft(_ghost, 8);
        Canvas.SetTop(_ghost, 0);
        DragLayer.Children.Add(_ghost);
    }

    private void UpdateDrag(MouseEventArgs e)
    {
        if (_ghost is not null)
        {
            var layerPoint = e.GetPosition(DragLayer);
            Canvas.SetTop(_ghost, Math.Max(0, layerPoint.Y - 28));
        }

        if (_dragItem is null || ViewModel is null)
        {
            return;
        }

        var pointerY = e.GetPosition(LayoutItems).Y;
        var target = ComputeTargetIndex(pointerY);
        var from = ViewModel.LayoutBlocks.IndexOf(_dragItem);
        if (target < 0 || target == _lastTargetIndex || target == from || target == from + 1)
        {
            return;
        }

        ViewModel.MoveLayoutBlockToIndex(_dragItem, target);
        _lastTargetIndex = ViewModel.LayoutBlocks.IndexOf(_dragItem);
        LayoutItems.UpdateLayout();
    }

    /// <summary>指针越过其他块的后半程时立即让位，实现实时预览。</summary>
    private int ComputeTargetIndex(double pointerY)
    {
        var blocks = ViewModel?.LayoutBlocks;
        if (blocks is null)
        {
            return -1;
        }

        for (var i = 0; i < blocks.Count; i++)
        {
            if (ReferenceEquals(blocks[i], _dragItem))
            {
                continue;
            }

            if (LayoutItems.ItemContainerGenerator.ContainerFromIndex(i) is not FrameworkElement container)
            {
                continue;
            }

            var top = container.TranslatePoint(new Point(0, 0), LayoutItems).Y;
            if (pointerY < top + container.ActualHeight / 2)
            {
                return i;
            }
        }

        return blocks.Count;
    }

    private void AutoScroll(MouseEventArgs e)
    {
        if (_scrollViewer is null)
        {
            return;
        }

        var position = e.GetPosition(_scrollViewer);
        const double edge = 56;
        if (position.Y < edge)
        {
            _scrollViewer.ScrollToVerticalOffset(_scrollViewer.VerticalOffset - 10);
        }
        else if (position.Y > _scrollViewer.ViewportHeight - edge)
        {
            _scrollViewer.ScrollToVerticalOffset(_scrollViewer.VerticalOffset + 10);
        }
    }

    private void FinishDrag()
    {
        if (_dragItem is not null)
        {
            _dragItem.IsDragging = false;
        }

        if (_ghost is not null)
        {
            DragLayer.Children.Remove(_ghost);
            _ghost = null;
        }

        if (_dragSource is not null)
        {
            _dragSource.ReleaseMouseCapture();
            _dragSource = null;
        }

        _dragItem = null;
        _dragging = false;
        _lastTargetIndex = -1;
        Cursor = Cursors.Arrow;
    }

    private static T? FindAncestor<T>(DependencyObject element) where T : DependencyObject
    {
        var current = VisualTreeHelper.GetParent(element);
        while (current is not null)
        {
            if (current is T match)
            {
                return match;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }

    private void Reset_Click(object sender, RoutedEventArgs e) => ViewModel?.ResetLayout();
}
