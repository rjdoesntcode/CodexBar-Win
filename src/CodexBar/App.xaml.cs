using System.Windows;
using CodexBar.Core.Browser;
using CodexBar.Core.Models;
using CodexBar.Core.Providers;
using CodexBar.ViewModels;

namespace CodexBar;

public partial class App : Application
{
    private TrayIconManager? _trayIcon;
    private ProviderManager? _providerManager;
    private AppSettings? _settings;
    private Timer? _refreshTimer;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Load settings
        _settings = await AppSettings.LoadAsync();

        // Initialize services
        var cookieService = new BrowserCookieService(_settings.PreferredBrowser);
        _providerManager = new ProviderManager(cookieService);

        // Initialize tray icon
        _trayIcon = new TrayIconManager(_providerManager, _settings);
        _trayIcon.ExitRequested += OnExitRequested;
        _trayIcon.SettingsRequested += OnSettingsRequested;
        _trayIcon.RefreshRequested += OnRefreshRequested;

        // Start refresh timer
        StartRefreshTimer();

        // Initial refresh
        await RefreshAllAsync();
    }

    private void StartRefreshTimer()
    {
        var interval = TimeSpan.FromMinutes(_settings?.RefreshIntervalMinutes ?? 5);
        _refreshTimer = new Timer(
            async _ => await RefreshAllAsync(),
            null,
            interval,
            interval);
    }

    private async Task RefreshAllAsync()
    {
        if (_providerManager == null || _settings == null)
            return;

        try
        {
            await _providerManager.RefreshAllAsync(_settings);
            _trayIcon?.UpdateStatus();
        }
        catch
        {
            // Silently ignore refresh errors
        }
    }

    private void OnExitRequested(object? sender, EventArgs e)
    {
        _refreshTimer?.Dispose();
        _trayIcon?.Dispose();
        Shutdown();
    }

    private void OnSettingsRequested(object? sender, EventArgs e)
    {
        var settingsWindow = new SettingsWindow(_settings!, _providerManager!);
        settingsWindow.SettingsChanged += async (_, _) =>
        {
            await _settings!.SaveAsync();
            _refreshTimer?.Dispose();
            StartRefreshTimer();
            await RefreshAllAsync();
        };
        settingsWindow.ShowDialog();
    }

    private async void OnRefreshRequested(object? sender, EventArgs e)
    {
        await RefreshAllAsync();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _refreshTimer?.Dispose();
        _trayIcon?.Dispose();
        base.OnExit(e);
    }
}
