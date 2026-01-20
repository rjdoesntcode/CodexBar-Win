using System.Windows;
using System.Windows.Controls;
using CodexBar.Core.Models;
using CodexBar.Core.Providers;
using Microsoft.Win32;

namespace CodexBar;

public partial class SettingsWindow : Window
{
    private readonly AppSettings _settings;
    private readonly ProviderManager _providerManager;
    private readonly Dictionary<ProviderType, CheckBox> _providerCheckboxes = new();

    public event EventHandler? SettingsChanged;

    public SettingsWindow(AppSettings settings, ProviderManager providerManager)
    {
        InitializeComponent();
        _settings = settings;
        _providerManager = providerManager;

        LoadSettings();
        PopulateProviders();
    }

    private void LoadSettings()
    {
        // Refresh interval
        for (int i = 0; i < RefreshIntervalCombo.Items.Count; i++)
        {
            if (RefreshIntervalCombo.Items[i] is ComboBoxItem item &&
                item.Content?.ToString() == _settings.RefreshIntervalMinutes.ToString())
            {
                RefreshIntervalCombo.SelectedIndex = i;
                break;
            }
        }

        if (RefreshIntervalCombo.SelectedIndex < 0)
            RefreshIntervalCombo.SelectedIndex = 2; // Default to 5 minutes

        // Notifications
        WarningNotificationsCheck.IsChecked = _settings.ShowWarningNotifications;
        ExceededNotificationsCheck.IsChecked = _settings.ShowExceededNotifications;

        // Browser
        BrowserCombo.SelectedIndex = (int)_settings.PreferredBrowser;

        // Startup
        StartWithWindowsCheck.IsChecked = _settings.StartWithWindows;
    }

    private void PopulateProviders()
    {
        foreach (var provider in _providerManager.RegisteredProviders)
        {
            var checkbox = new CheckBox
            {
                Content = provider.GetDisplayName(),
                IsChecked = _settings.IsProviderEnabled(provider),
                Foreground = FindResource("TextSecondaryBrush") as System.Windows.Media.Brush,
                Margin = new Thickness(0, 0, 0, 8)
            };

            _providerCheckboxes[provider] = checkbox;
            ProvidersPanel.Children.Add(checkbox);
        }
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        // Save refresh interval
        if (RefreshIntervalCombo.SelectedItem is ComboBoxItem item &&
            int.TryParse(item.Content?.ToString(), out var interval))
        {
            _settings.RefreshIntervalMinutes = interval;
        }

        // Save notifications
        _settings.ShowWarningNotifications = WarningNotificationsCheck.IsChecked ?? false;
        _settings.ShowExceededNotifications = ExceededNotificationsCheck.IsChecked ?? false;

        // Save browser
        _settings.PreferredBrowser = (BrowserType)BrowserCombo.SelectedIndex;

        // Save providers
        foreach (var (provider, checkbox) in _providerCheckboxes)
        {
            _settings.SetProviderEnabled(provider, checkbox.IsChecked ?? false);
        }

        // Save startup
        _settings.StartWithWindows = StartWithWindowsCheck.IsChecked ?? false;
        UpdateStartupRegistry(_settings.StartWithWindows);

        SettingsChanged?.Invoke(this, EventArgs.Empty);
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private static void UpdateStartupRegistry(bool enable)
    {
        const string appName = "CodexBar";
        var exePath = Environment.ProcessPath;

        if (string.IsNullOrEmpty(exePath))
            return;

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);

            if (key == null)
                return;

            if (enable)
            {
                key.SetValue(appName, $"\"{exePath}\"");
            }
            else
            {
                key.DeleteValue(appName, false);
            }
        }
        catch
        {
            // Silently fail if registry access is denied
        }
    }
}
