using System.Text.Json;
using System.Text.Json.Serialization;

namespace CodexBar.Core.Models;

/// <summary>
/// Application settings
/// </summary>
public class AppSettings
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "CodexBar",
        "settings.json");

    /// <summary>
    /// Refresh interval in minutes
    /// </summary>
    public int RefreshIntervalMinutes { get; set; } = 5;

    /// <summary>
    /// Whether to start with Windows
    /// </summary>
    public bool StartWithWindows { get; set; } = false;

    /// <summary>
    /// Whether to show notifications for rate limit warnings
    /// </summary>
    public bool ShowWarningNotifications { get; set; } = true;

    /// <summary>
    /// Whether to show notifications when rate limit is exceeded
    /// </summary>
    public bool ShowExceededNotifications { get; set; } = true;

    /// <summary>
    /// Preferred browser for cookie import
    /// </summary>
    public BrowserType PreferredBrowser { get; set; } = BrowserType.Chrome;

    /// <summary>
    /// Enabled providers
    /// </summary>
    public Dictionary<ProviderType, bool> EnabledProviders { get; set; } = new()
    {
        [ProviderType.Claude] = true,
        [ProviderType.Codex] = true,
        [ProviderType.Cursor] = true,
        [ProviderType.Copilot] = true
    };

    /// <summary>
    /// Whether a provider is enabled
    /// </summary>
    public bool IsProviderEnabled(ProviderType provider)
    {
        return EnabledProviders.TryGetValue(provider, out var enabled) && enabled;
    }

    /// <summary>
    /// Sets whether a provider is enabled
    /// </summary>
    public void SetProviderEnabled(ProviderType provider, bool enabled)
    {
        EnabledProviders[provider] = enabled;
    }

    /// <summary>
    /// Saves settings to disk
    /// </summary>
    public async Task SaveAsync()
    {
        var dir = Path.GetDirectoryName(SettingsPath);
        if (dir != null && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() }
        };

        var json = JsonSerializer.Serialize(this, options);
        await File.WriteAllTextAsync(SettingsPath, json);
    }

    /// <summary>
    /// Loads settings from disk
    /// </summary>
    public static async Task<AppSettings> LoadAsync()
    {
        if (!File.Exists(SettingsPath))
        {
            return new AppSettings();
        }

        try
        {
            var json = await File.ReadAllTextAsync(SettingsPath);
            var options = new JsonSerializerOptions
            {
                Converters = { new JsonStringEnumConverter() }
            };
            return JsonSerializer.Deserialize<AppSettings>(json, options) ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }
}

/// <summary>
/// Supported browsers for cookie import
/// </summary>
public enum BrowserType
{
    Chrome,
    Edge,
    Firefox,
    Brave,
    Opera
}
