using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using ComboBox = System.Windows.Controls.ComboBox;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using UserControl = System.Windows.Controls.UserControl;

namespace NineMRoom.Client.App;

public partial class AppEditorView : UserControl
{
    private bool _syncingCategory;
    private MainViewModel? _viewModel;

    public AppEditorView()
    {
        InitializeComponent();
        Loaded += (_, _) => Attach();
        DataContextChanged += (_, _) => Attach();
        Unloaded += (_, _) => Detach();
    }

    private MainViewModel? ViewModel => DataContext as MainViewModel;

    private void Attach()
    {
        var next = ViewModel;
        if (ReferenceEquals(next, _viewModel))
        {
            RebuildCategoryOptions();
            return;
        }

        Detach();
        _viewModel = next;
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;
            _viewModel.AppCategories.CollectionChanged += Categories_CollectionChanged;
        }

        RebuildCategoryOptions();
    }

    private void Detach()
    {
        if (_viewModel is null)
        {
            return;
        }

        _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
        _viewModel.AppCategories.CollectionChanged -= Categories_CollectionChanged;
        _viewModel = null;
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.AppDraft))
        {
            RebuildCategoryOptions();
        }
    }

    private void Categories_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        RebuildCategoryOptions();

    private void RebuildCategoryOptions()
    {
        if (CategoryBox is null)
        {
            return;
        }

        var vm = ViewModel;
        if (vm is null)
        {
            return;
        }

        _syncingCategory = true;
        try
        {
            var options = new List<CategoryOption> { new(string.Empty, "自动（按应用推断）") };
            options.AddRange(vm.AppCategories.Select(category => new CategoryOption(category.Id, category.Name)));
            CategoryBox.ItemsSource = options;

            var current = vm.AppDraft?.CategoryOverride ?? string.Empty;
            CategoryBox.SelectedItem = options.FirstOrDefault(option =>
                string.Equals(option.Id, current, StringComparison.Ordinal)) ?? options[0];
        }
        finally
        {
            _syncingCategory = false;
        }
    }

    private void CategoryBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncingCategory || CategoryBox.SelectedItem is not CategoryOption option)
        {
            return;
        }

        if (ViewModel?.AppDraft is { } draft)
        {
            draft.CategoryOverride = option.Id;
        }
    }

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

    private sealed record CategoryOption(string Id, string Name);
}
