using System.Windows;
using System.Windows.Threading;
using MessageBox = System.Windows.MessageBox;
using WpfApplication = System.Windows.Application;

namespace NineMRoom.Client.App;

public partial class App : WpfApplication
{
    private SingleInstance? _singleInstance;
    private AppSettings _settings = new();
    private bool _isPrimary;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        _settings = SettingsStore.Load();
        ThemeService.Apply(_settings.Theme);
        if (!ThemeService.IsLight(_settings.Theme))
        {
            NativeTheme.EnableDarkMenus();
        }

        _singleInstance = new SingleInstance();
        _isPrimary = _singleInstance.IsFirst;

        if (!_isPrimary)
        {
            SingleInstance.NotifyExisting();
            Shutdown();
            return;
        }

        LogService.Write($"startup primary={_isPrimary}");

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            LogService.Write($"unhandled exception: {args.ExceptionObject}");
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            LogService.Write($"unobserved task exception: {args.Exception}");
            args.SetObserved();
        };

        _singleInstance.ActivateRequested += () => Dispatcher.Invoke(() =>
        {
            if (MainWindow is MainWindow window)
            {
                window.ShowFromTray();
            }
        });

        var startMinimized = e.Args.Any(argument =>
            argument.Equals("--minimized", StringComparison.OrdinalIgnoreCase));

        var window = new MainWindow(_settings, startMinimized);
        MainWindow = window;

        if (!startMinimized || !window.TrayAvailable)
        {
            window.Show();
        }
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        LogService.Write($"dispatcher exception: {e.Exception}");

        MessageBox.Show(
            $"9mRoom 遇到未处理的错误，已写入日志。\n\n{e.Exception.Message}",
            "9mRoom",
            MessageBoxButton.OK,
            MessageBoxImage.Error);

        e.Handled = true;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_isPrimary)
        {
            SettingsStore.Save(_settings);
            LogService.Write("exit");
        }

        _singleInstance?.Dispose();
        base.OnExit(e);
    }
}
