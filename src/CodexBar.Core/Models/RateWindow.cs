namespace CodexBar.Core.Models;

/// <summary>
/// Represents a rate limit window with usage tracking
/// </summary>
public record RateWindow
{
    /// <summary>
    /// Name of the rate window (e.g., "Session", "Weekly", "Opus")
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Current usage as a percentage (0.0 to 1.0+)
    /// </summary>
    public double UsagePercent { get; init; }

    /// <summary>
    /// When this rate window resets
    /// </summary>
    public DateTime? ResetsAt { get; init; }

    /// <summary>
    /// Duration of the rate window
    /// </summary>
    public TimeSpan? WindowDuration { get; init; }

    /// <summary>
    /// Human-readable description of time until reset
    /// </summary>
    public string TimeUntilReset => ResetsAt.HasValue
        ? FormatTimeUntil(ResetsAt.Value - DateTime.UtcNow)
        : "Unknown";

    /// <summary>
    /// Whether the rate limit has been exceeded
    /// </summary>
    public bool IsExceeded => UsagePercent >= 1.0;

    /// <summary>
    /// Whether usage is approaching the limit (>80%)
    /// </summary>
    public bool IsWarning => UsagePercent >= 0.8 && UsagePercent < 1.0;

    private static string FormatTimeUntil(TimeSpan duration)
    {
        if (duration.TotalSeconds <= 0)
            return "Now";
        if (duration.TotalMinutes < 1)
            return $"{(int)duration.TotalSeconds}s";
        if (duration.TotalHours < 1)
            return $"{(int)duration.TotalMinutes}m";
        if (duration.TotalDays < 1)
            return $"{(int)duration.TotalHours}h {duration.Minutes}m";
        return $"{(int)duration.TotalDays}d {duration.Hours}h";
    }
}

/// <summary>
/// Represents a snapshot of usage across multiple rate windows
/// </summary>
public record UsageSnapshot
{
    /// <summary>
    /// The provider this snapshot is for
    /// </summary>
    public required ProviderType Provider { get; init; }

    /// <summary>
    /// When this snapshot was captured
    /// </summary>
    public DateTime CapturedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Primary rate window (e.g., session limit)
    /// </summary>
    public RateWindow? Primary { get; init; }

    /// <summary>
    /// Secondary rate window (e.g., weekly limit)
    /// </summary>
    public RateWindow? Secondary { get; init; }

    /// <summary>
    /// Additional rate windows (e.g., model-specific limits)
    /// </summary>
    public IReadOnlyList<RateWindow> Additional { get; init; } = [];

    /// <summary>
    /// User email or identifier
    /// </summary>
    public string? UserIdentity { get; init; }

    /// <summary>
    /// Plan name (e.g., "Pro", "Max", "Enterprise")
    /// </summary>
    public string? PlanName { get; init; }

    /// <summary>
    /// Extra usage cost in dollars (if applicable)
    /// </summary>
    public decimal? ExtraUsageCost { get; init; }

    /// <summary>
    /// Extra usage limit in dollars (if applicable)
    /// </summary>
    public decimal? ExtraUsageLimit { get; init; }

    /// <summary>
    /// Error message if fetching failed
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// Whether this snapshot contains valid data
    /// </summary>
    public bool IsValid => Error == null && (Primary != null || Secondary != null);

    /// <summary>
    /// The most critical rate window (highest usage)
    /// </summary>
    public RateWindow? MostCritical => GetAllWindows()
        .OrderByDescending(w => w.UsagePercent)
        .FirstOrDefault();

    private IEnumerable<RateWindow> GetAllWindows()
    {
        if (Primary != null) yield return Primary;
        if (Secondary != null) yield return Secondary;
        foreach (var window in Additional) yield return window;
    }
}
