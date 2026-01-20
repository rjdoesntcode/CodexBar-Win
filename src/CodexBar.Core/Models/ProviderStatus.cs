namespace CodexBar.Core.Models;

/// <summary>
/// Overall status of a provider
/// </summary>
public enum ProviderStatusLevel
{
    Unknown,
    Healthy,
    Warning,
    Critical,
    Error,
    Disabled
}

/// <summary>
/// Represents the current status of a provider
/// </summary>
public record ProviderStatus
{
    /// <summary>
    /// The provider this status is for
    /// </summary>
    public required ProviderType Provider { get; init; }

    /// <summary>
    /// Whether this provider is enabled
    /// </summary>
    public bool IsEnabled { get; init; } = true;

    /// <summary>
    /// Current status level
    /// </summary>
    public ProviderStatusLevel Level { get; init; } = ProviderStatusLevel.Unknown;

    /// <summary>
    /// Latest usage snapshot
    /// </summary>
    public UsageSnapshot? Usage { get; init; }

    /// <summary>
    /// Last time data was successfully refreshed
    /// </summary>
    public DateTime? LastRefreshed { get; init; }

    /// <summary>
    /// Error message if status is Error
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Whether authentication is configured
    /// </summary>
    public bool IsAuthenticated { get; init; }

    /// <summary>
    /// Gets a summary string for display
    /// </summary>
    public string GetSummary()
    {
        if (!IsEnabled) return "Disabled";
        if (!IsAuthenticated) return "Not configured";
        if (ErrorMessage != null) return $"Error: {ErrorMessage}";
        if (Usage?.MostCritical == null) return "No data";

        var critical = Usage.MostCritical;
        var percent = (int)(critical.UsagePercent * 100);
        return $"{percent}% ({critical.TimeUntilReset})";
    }

    /// <summary>
    /// Creates a status from a usage snapshot
    /// </summary>
    public static ProviderStatus FromUsage(ProviderType provider, UsageSnapshot? usage, bool isEnabled = true)
    {
        var level = ProviderStatusLevel.Unknown;
        string? error = null;

        if (usage == null)
        {
            level = ProviderStatusLevel.Unknown;
        }
        else if (usage.Error != null)
        {
            level = ProviderStatusLevel.Error;
            error = usage.Error;
        }
        else if (usage.MostCritical?.IsExceeded == true)
        {
            level = ProviderStatusLevel.Critical;
        }
        else if (usage.MostCritical?.IsWarning == true)
        {
            level = ProviderStatusLevel.Warning;
        }
        else
        {
            level = ProviderStatusLevel.Healthy;
        }

        return new ProviderStatus
        {
            Provider = provider,
            IsEnabled = isEnabled,
            Level = level,
            Usage = usage,
            LastRefreshed = usage?.CapturedAt,
            ErrorMessage = error,
            IsAuthenticated = usage?.Error == null || !usage.Error.Contains("not configured")
        };
    }
}
