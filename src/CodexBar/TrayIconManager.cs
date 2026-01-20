using System.Drawing;
using System.Windows.Forms;
using CodexBar.Core.Models;
using CodexBar.Core.Providers;

namespace CodexBar;

/// <summary>
/// Manages the system tray icon and context menu
/// </summary>
public class TrayIconManager : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly ProviderManager _providerManager;
    private readonly AppSettings _settings;
    private readonly ContextMenuStrip _contextMenu;

    public event EventHandler? ExitRequested;
    public event EventHandler? SettingsRequested;
    public event EventHandler? RefreshRequested;

    public TrayIconManager(ProviderManager providerManager, AppSettings settings)
    {
        _providerManager = providerManager;
        _settings = settings;

        _contextMenu = CreateContextMenu();
        _notifyIcon = new NotifyIcon
        {
            Icon = CreateIcon(ProviderStatusLevel.Unknown),
            Visible = true,
            Text = "CodexBar - AI Usage Monitor",
            ContextMenuStrip = _contextMenu
        };

        _notifyIcon.DoubleClick += (_, _) => RefreshRequested?.Invoke(this, EventArgs.Empty);

        _providerManager.AllRefreshed += (_, _) => UpdateStatus();
        _providerManager.StatusChanged += OnStatusChanged;
    }

    private ContextMenuStrip CreateContextMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Opening += (_, _) => UpdateMenuItems(menu);

        // Header
        var headerItem = new ToolStripLabel("CodexBar - AI Usage Monitor")
        {
            Font = new Font(menu.Font, FontStyle.Bold)
        };
        menu.Items.Add(headerItem);
        menu.Items.Add(new ToolStripSeparator());

        // Provider status items (will be populated dynamically)
        foreach (var provider in _providerManager.RegisteredProviders)
        {
            if (_settings.IsProviderEnabled(provider))
            {
                var item = new ToolStripMenuItem(provider.GetDisplayName())
                {
                    Tag = provider
                };
                menu.Items.Add(item);
            }
        }

        menu.Items.Add(new ToolStripSeparator());

        // Refresh
        var refreshItem = new ToolStripMenuItem("Refresh Now", null, (_, _) => RefreshRequested?.Invoke(this, EventArgs.Empty));
        menu.Items.Add(refreshItem);

        // Settings
        var settingsItem = new ToolStripMenuItem("Settings...", null, (_, _) => SettingsRequested?.Invoke(this, EventArgs.Empty));
        menu.Items.Add(settingsItem);

        menu.Items.Add(new ToolStripSeparator());

        // Exit
        var exitItem = new ToolStripMenuItem("Exit", null, (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty));
        menu.Items.Add(exitItem);

        return menu;
    }

    private void UpdateMenuItems(ContextMenuStrip menu)
    {
        foreach (ToolStripItem item in menu.Items)
        {
            if (item is ToolStripMenuItem menuItem && item.Tag is ProviderType provider)
            {
                var status = _providerManager.GetStatus(provider);
                menuItem.Text = $"{provider.GetDisplayName()}: {status.GetSummary()}";
                menuItem.Image = CreateStatusBitmap(status.Level);
            }
        }
    }

    public void UpdateStatus()
    {
        var overallStatus = _providerManager.GetOverallStatus();
        _notifyIcon.Icon = CreateIcon(overallStatus);

        // Update tooltip
        var tooltipLines = new List<string> { "CodexBar" };
        foreach (var provider in _providerManager.RegisteredProviders)
        {
            if (_settings.IsProviderEnabled(provider))
            {
                var status = _providerManager.GetStatus(provider);
                tooltipLines.Add($"{provider.GetDisplayName()}: {status.GetSummary()}");
            }
        }

        // Truncate to 63 chars (NotifyIcon limit)
        var tooltip = string.Join("\n", tooltipLines);
        _notifyIcon.Text = tooltip.Length > 63 ? tooltip[..60] + "..." : tooltip;
    }

    private void OnStatusChanged(object? sender, ProviderStatusChangedEventArgs e)
    {
        // Show notification for significant changes
        if (e.NewStatus.Level == ProviderStatusLevel.Critical && e.OldStatus.Level != ProviderStatusLevel.Critical)
        {
            if (_settings.ShowExceededNotifications)
            {
                ShowNotification(
                    $"{e.Provider.GetDisplayName()} Rate Limit Exceeded",
                    $"You have exceeded your rate limit. Resets in {e.NewStatus.Usage?.MostCritical?.TimeUntilReset ?? "unknown"}",
                    ToolTipIcon.Warning);
            }
        }
        else if (e.NewStatus.Level == ProviderStatusLevel.Warning && e.OldStatus.Level == ProviderStatusLevel.Healthy)
        {
            if (_settings.ShowWarningNotifications)
            {
                var percent = (int)((e.NewStatus.Usage?.MostCritical?.UsagePercent ?? 0) * 100);
                ShowNotification(
                    $"{e.Provider.GetDisplayName()} Usage Warning",
                    $"You are at {percent}% of your rate limit",
                    ToolTipIcon.Info);
            }
        }

        UpdateStatus();
    }

    private void ShowNotification(string title, string message, ToolTipIcon icon)
    {
        _notifyIcon.ShowBalloonTip(5000, title, message, icon);
    }

    private static Icon CreateIcon(ProviderStatusLevel status)
    {
        var bitmap = new Bitmap(16, 16);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        var color = status switch
        {
            ProviderStatusLevel.Healthy => Color.FromArgb(16, 185, 129), // Green
            ProviderStatusLevel.Warning => Color.FromArgb(245, 158, 11), // Yellow
            ProviderStatusLevel.Critical => Color.FromArgb(239, 68, 68), // Red
            ProviderStatusLevel.Error => Color.FromArgb(107, 114, 128), // Gray
            _ => Color.FromArgb(156, 163, 175) // Light gray
        };

        using var brush = new SolidBrush(color);
        graphics.FillEllipse(brush, 2, 2, 12, 12);

        // Add border
        using var pen = new Pen(Color.White, 1);
        graphics.DrawEllipse(pen, 2, 2, 12, 12);

        return Icon.FromHandle(bitmap.GetHicon());
    }

    private static Bitmap CreateStatusBitmap(ProviderStatusLevel status)
    {
        var bitmap = new Bitmap(16, 16);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        var color = status switch
        {
            ProviderStatusLevel.Healthy => Color.FromArgb(16, 185, 129),
            ProviderStatusLevel.Warning => Color.FromArgb(245, 158, 11),
            ProviderStatusLevel.Critical => Color.FromArgb(239, 68, 68),
            ProviderStatusLevel.Error => Color.FromArgb(107, 114, 128),
            _ => Color.FromArgb(156, 163, 175)
        };

        using var brush = new SolidBrush(color);
        graphics.FillEllipse(brush, 2, 2, 12, 12);

        return bitmap;
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _contextMenu.Dispose();
    }
}
