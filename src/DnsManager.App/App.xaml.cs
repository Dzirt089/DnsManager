using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using DnsManager.App.Logging;
using DnsManager.App.Services;
using DnsManager.App.ViewModels;
using DnsManager.Core.Logging;
using DnsManager.Core.PowerShell;
using DnsManager.Core.Services;
using Hardcodet.Wpf.TaskbarNotification;

namespace DnsManager.App;

public partial class App : Application
{
    private MainWindow? _window;
    private MainViewModel? _vm;
    private LogService? _log;
    private TaskbarIcon? _tray;
    private bool _isExiting;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Composition root (без DI-контейнера — приложение небольшое).
        _log = new LogService();
        var runner = new ProcessPowerShellRunner();
        var networkService = new NetworkService(runner);
        var dnsService = new DnsService(runner, _log);

        var presetsVm = new PresetsViewModel(new PresetStore(), _log);
        var resolutionVm = new ResolutionViewModel(new DnsResolver(), _log);
        var benchmarkVm = new BenchmarkViewModel(new DnsBenchmarkService(), _log);

        _vm = new MainViewModel(networkService, dnsService, _log, new AutostartService(),
            presetsVm, resolutionVm, benchmarkVm);

        _window = new MainWindow { DataContext = _vm };
        _window.Closing += OnWindowClosing;
        _window.Show();

        SetupTray();
        _log.Info(LogEvents.AppStartup, "Приложение запущено. Требуются права администратора.");

        // Первичная загрузка адаптеров.
        _ = _vm.RefreshAdaptersCommand.ExecuteAsync(null);
    }

    private void SetupTray()
    {
        using var stream = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/app.ico"))?.Stream;
        var icon = stream is null ? System.Drawing.SystemIcons.Application : new System.Drawing.Icon(stream);

        _tray = new TaskbarIcon
        {
            Icon = icon,
            ToolTipText = "DNS Manager"
        };

        var menu = new ContextMenu();
        menu.Items.Add(MakeMenuItem("Показать окно", (_, _) => ShowMainWindow()));
        menu.Items.Add(MakeMenuItem("Включить DNS", async (_, _) => await _vm!.EnableDnsCommand.ExecuteAsync(null)));
        menu.Items.Add(MakeMenuItem("Выключить DNS (DHCP)", async (_, _) => await _vm!.DisableDnsCommand.ExecuteAsync(null)));
        menu.Items.Add(new Separator());
        menu.Items.Add(MakeMenuItem("Выход", (_, _) => ExitApp()));
        _tray.ContextMenu = menu;

        _tray.TrayMouseDoubleClick += (_, _) => ShowMainWindow();
    }

    private static MenuItem MakeMenuItem(string header, RoutedEventHandler handler)
    {
        var item = new MenuItem { Header = header };
        item.Click += handler;
        return item;
    }

    private void ShowMainWindow()
    {
        if (_window is null)
            return;
        _window.Show();
        if (_window.WindowState == WindowState.Minimized)
            _window.WindowState = WindowState.Normal;
        _window.Activate();
    }

    private void OnWindowClosing(object? sender, CancelEventArgs e)
    {
        if (_isExiting)
            return;
        // Закрытие окна = свёртка в трей.
        e.Cancel = true;
        _window?.Hide();
    }

    private void ExitApp()
    {
        _isExiting = true;
        _tray?.Dispose();
        _log?.Dispose();
        Shutdown();
    }
}
