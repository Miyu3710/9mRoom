using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using Brush = System.Windows.Media.Brush;
using Button = System.Windows.Controls.Button;
using Cursors = System.Windows.Input.Cursors;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using Point = System.Windows.Point;
using UserControl = System.Windows.Controls.UserControl;

namespace NineMRoom.Client.App;

public partial class LayoutView : UserControl
{
    private Point _dragStart;
    private StatusBlockItem? _dragItem;
    private UIElement? _dragSource;
    private bool _dragging;
    private System.Windows.Controls.Border? _ghost;
    private System.Windows.Controls.Border? _insertionLine;
    private int _insertIndex;

    public LayoutView()
    {
        InitializeComponent();
    }

    private MainViewModel? ViewModel => DataContext as MainViewModel;

    private void AddBlock_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string type })
        {
            ViewModel?.AddLayoutBlock(type);
        }
    }

    private void AddCustom_Click(object sender, RoutedEventArgs e) => ViewModel?.AddCustomBlock();

    private void Help_Click(object sender, RoutedEventArgs e) => HelpPopup.IsOpen = !HelpPopup.IsOpen;

    private void Remove_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: StatusBlockItem item })
        {
            ViewModel?.RemoveLayoutBlock(item);
        }
    }

    private void DragHandle_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStart = e.GetPosition(null);
        _dragItem = (sender as FrameworkElement)?.DataContext as StatusBlockItem;
        _dragSource = sender as UIElement;
        _dragging = false;
        _dragSource?.CaptureMouse();
        e.Handled = true;
    }

    private void DragHandle_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _dragItem is null || _dragSource is null)
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

    private void DragHandle_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_dragging && _dragItem is not null)
        {
            ViewModel?.MoveLayoutBlockToIndex(_dragItem, _insertIndex);
        }

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

        var label = new System.Windows.Controls.TextBlock
        {
            Text = _dragItem.Label,
            FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)FindResource("BrushText")
        };

        var template = new System.Windows.Controls.TextBlock
        {
            Text = string.IsNullOrWhiteSpace(_dragItem.Template) ? "（空模板）" : _dragItem.Template,
            FontSize = 12,
            Foreground = (Brush)FindResource("BrushTextMuted"),
            TextTrimming = TextTrimming.CharacterEllipsis,
            Margin = new Thickness(0, 2, 0, 0)
        };

        _ghost = new System.Windows.Controls.Border
        {
            Background = (Brush)FindResource("BrushGlassSurface"),
            BorderBrush = (Brush)FindResource("BrushAccent"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(12, 8, 12, 8),
            Width = Math.Max(260, LayoutItems.ActualWidth - 16),
            Opacity = 0.95,
            IsHitTestVisible = false,
            Effect = new DropShadowEffect { BlurRadius = 18, ShadowDepth = 4, Opacity = 0.45 },
            Child = new System.Windows.Controls.StackPanel
            {
                Children = { label, template }
            }
        };

        _insertionLine = new System.Windows.Controls.Border
        {
            Height = 2,
            Width = Math.Max(0, LayoutItems.ActualWidth),
            Background = (Brush)FindResource("BrushAccent"),
            CornerRadius = new CornerRadius(1),
            IsHitTestVisible = false
        };

        System.Windows.Controls.Canvas.SetLeft(_ghost, 8);
        System.Windows.Controls.Canvas.SetLeft(_insertionLine, 0);
        DragLayer.Children.Add(_ghost);
        DragLayer.Children.Add(_insertionLine);
    }

    private void UpdateDrag(Point pointer)
    {
        if (_ghost is not null)
        {
            var top = Math.Clamp(pointer.Y - 22, 0, Math.Max(0, DragLayer.ActualHeight - 50));
            System.Windows.Controls.Canvas.SetTop(_ghost, top);
        }

        var pointerInItems = Mouse.GetPosition(LayoutItems);
        var (index, y) = FindDropTarget(pointerInItems.Y);
        _insertIndex = index;

        if (_insertionLine is not null)
        {
            _insertionLine.Width = Math.Max(0, LayoutItems.ActualWidth);
            System.Windows.Controls.Canvas.SetTop(_insertionLine, Math.Max(0, y));
        }
    }

    private (int Index, double Y) FindDropTarget(double pointerY)
    {
        var count = LayoutItems.Items.Count;
        var lastBottom = 0.0;

        for (var i = 0; i < count; i++)
        {
            if (LayoutItems.ItemContainerGenerator.ContainerFromIndex(i) is not FrameworkElement container)
            {
                continue;
            }

            var top = container.TranslatePoint(new Point(0, 0), LayoutItems).Y;
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
        if (_dragItem is not null)
        {
            _dragItem.IsDragging = false;
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
        _dragItem = null;
        _dragging = false;
        Cursor = Cursors.Arrow;
    }

    private void Reset_Click(object sender, RoutedEventArgs e) => ViewModel?.ResetLayout();
}
