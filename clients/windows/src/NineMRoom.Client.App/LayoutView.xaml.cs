using System.Windows;
using System.Windows.Controls;
using GongSolutions.Wpf.DragDrop;
using DragDropEffects = System.Windows.DragDropEffects;
using IDropTarget = GongSolutions.Wpf.DragDrop.IDropTarget;
using UserControl = System.Windows.Controls.UserControl;

namespace NineMRoom.Client.App;

public partial class LayoutView : UserControl, IDropTarget
{
    public LayoutView()
    {
        InitializeComponent();
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

    private void Reset_Click(object sender, RoutedEventArgs e) => ViewModel?.ResetLayout();

    void IDropTarget.DragOver(IDropInfo dropInfo)
    {
        if (dropInfo.Data is not StatusBlockItem item || ViewModel is null)
        {
            return;
        }

        dropInfo.Effects = DragDropEffects.Move;
        // 实时让位：不显示插入线，直接移动集合项。
        dropInfo.DropTargetAdorner = null;

        var blocks = ViewModel.LayoutBlocks;
        var from = blocks.IndexOf(item);
        var target = dropInfo.InsertIndex;
        if (from < 0 || target < 0 || target == from || target == from + 1)
        {
            return;
        }

        ViewModel.MoveLayoutBlockToIndex(item, target);
        LayoutItems.UpdateLayout();
    }

    void IDropTarget.Drop(IDropInfo dropInfo)
    {
        dropInfo.Effects = DragDropEffects.Move;
    }
}
