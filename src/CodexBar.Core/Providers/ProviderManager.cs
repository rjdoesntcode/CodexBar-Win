using CodexBar.Core.Browser;
using CodexBar.Core.Models;
using CodexBar.Core.Providers.Claude;
using CodexBar.Core.Providers.Codex;
using CodexBar.Core.Providers.Copilot;
using CodexBar.Core.Providers.Cursor;

namespace CodexBar.Core.Providers;

/// <summary>
/// Manages all usage providers
/// </summary>
public class ProviderManager
{
    private readonly Dictionary<ProviderType, IUsageProvider> _providers;
    private readonly Dictionary<ProviderType, ProviderStatus> _statusCache;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    public event EventHandler<ProviderStatusChangedEventArgs>? StatusChanged;
    public event EventHandler<AllProvidersRefreshedEventArgs>? AllRefreshed;

    public ProviderManager(BrowserCookieService cookieService)
    {
        var httpClient = new HttpClient();

        _providers = new Dictionary<ProviderType, IUsageProvider>
        {
            [ProviderType.Claude] = new ClaudeUsageProvider(cookieService, httpClient),
            [ProviderType.Cursor] = new CursorUsageProvider(cookieService, httpClient),
            [ProviderType.Codex] = new CodexUsageProvider(),
            [ProviderType.Copilot] = new CopilotUsageProvider(httpClient)
        };

        _statusCache = new Dictionary<ProviderType, ProviderStatus>();

        foreach (var provider in _providers.Keys)
        {
            _statusCache[provider] = new ProviderStatus
            {
                Provider = provider,
                Level = ProviderStatusLevel.Unknown
            };
        }
    }

    /// <summary>
    /// Gets all registered providers
    /// </summary>
    public IReadOnlyList<ProviderType> RegisteredProviders => _providers.Keys.ToList();

    /// <summary>
    /// Gets the current status of a provider
    /// </summary>
    public ProviderStatus GetStatus(ProviderType provider)
    {
        return _statusCache.TryGetValue(provider, out var status)
            ? status
            : new ProviderStatus { Provider = provider, Level = ProviderStatusLevel.Unknown };
    }

    /// <summary>
    /// Gets all current statuses
    /// </summary>
    public IReadOnlyDictionary<ProviderType, ProviderStatus> GetAllStatuses()
    {
        return _statusCache;
    }

    /// <summary>
    /// Refreshes usage data for a specific provider
    /// </summary>
    public async Task RefreshAsync(ProviderType provider, CancellationToken cancellationToken = default)
    {
        if (!_providers.TryGetValue(provider, out var usageProvider))
            return;

        try
        {
            var usage = await usageProvider.FetchUsageAsync(cancellationToken);
            var newStatus = ProviderStatus.FromUsage(provider, usage);

            var oldStatus = _statusCache[provider];
            _statusCache[provider] = newStatus;

            if (oldStatus.Level != newStatus.Level || oldStatus.Usage?.MostCritical?.UsagePercent != newStatus.Usage?.MostCritical?.UsagePercent)
            {
                StatusChanged?.Invoke(this, new ProviderStatusChangedEventArgs(provider, oldStatus, newStatus));
            }
        }
        catch (Exception ex)
        {
            var errorStatus = new ProviderStatus
            {
                Provider = provider,
                Level = ProviderStatusLevel.Error,
                ErrorMessage = ex.Message
            };
            _statusCache[provider] = errorStatus;
            StatusChanged?.Invoke(this, new ProviderStatusChangedEventArgs(provider, _statusCache[provider], errorStatus));
        }
    }

    /// <summary>
    /// Refreshes all enabled providers
    /// </summary>
    public async Task RefreshAllAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        await _refreshLock.WaitAsync(cancellationToken);
        try
        {
            var tasks = new List<Task>();

            foreach (var provider in _providers.Keys)
            {
                if (settings.IsProviderEnabled(provider))
                {
                    tasks.Add(RefreshAsync(provider, cancellationToken));
                }
            }

            await Task.WhenAll(tasks);
            AllRefreshed?.Invoke(this, new AllProvidersRefreshedEventArgs(GetAllStatuses()));
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    /// <summary>
    /// Gets the most critical status across all providers
    /// </summary>
    public ProviderStatusLevel GetOverallStatus()
    {
        var statuses = _statusCache.Values.Where(s => s.IsEnabled).ToList();

        if (statuses.Any(s => s.Level == ProviderStatusLevel.Critical))
            return ProviderStatusLevel.Critical;

        if (statuses.Any(s => s.Level == ProviderStatusLevel.Error))
            return ProviderStatusLevel.Error;

        if (statuses.Any(s => s.Level == ProviderStatusLevel.Warning))
            return ProviderStatusLevel.Warning;

        if (statuses.All(s => s.Level == ProviderStatusLevel.Unknown))
            return ProviderStatusLevel.Unknown;

        return ProviderStatusLevel.Healthy;
    }
}

public class ProviderStatusChangedEventArgs : EventArgs
{
    public ProviderType Provider { get; }
    public ProviderStatus OldStatus { get; }
    public ProviderStatus NewStatus { get; }

    public ProviderStatusChangedEventArgs(ProviderType provider, ProviderStatus oldStatus, ProviderStatus newStatus)
    {
        Provider = provider;
        OldStatus = oldStatus;
        NewStatus = newStatus;
    }
}

public class AllProvidersRefreshedEventArgs : EventArgs
{
    public IReadOnlyDictionary<ProviderType, ProviderStatus> Statuses { get; }

    public AllProvidersRefreshedEventArgs(IReadOnlyDictionary<ProviderType, ProviderStatus> statuses)
    {
        Statuses = statuses;
    }
}
