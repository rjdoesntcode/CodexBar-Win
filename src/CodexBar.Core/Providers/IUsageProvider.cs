using CodexBar.Core.Models;

namespace CodexBar.Core.Providers;

/// <summary>
/// Interface for AI provider usage fetchers
/// </summary>
public interface IUsageProvider
{
    /// <summary>
    /// The provider type this fetcher handles
    /// </summary>
    ProviderType ProviderType { get; }

    /// <summary>
    /// Display name for the provider
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// Whether this provider is available (e.g., CLI installed, credentials present)
    /// </summary>
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches the current usage snapshot
    /// </summary>
    Task<UsageSnapshot> FetchUsageAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the authentication status
    /// </summary>
    Task<AuthStatus> GetAuthStatusAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Authentication status for a provider
/// </summary>
public record AuthStatus
{
    /// <summary>
    /// Whether authentication is configured
    /// </summary>
    public bool IsAuthenticated { get; init; }

    /// <summary>
    /// The authentication method being used
    /// </summary>
    public AuthMethod Method { get; init; }

    /// <summary>
    /// User email or identifier if available
    /// </summary>
    public string? UserIdentity { get; init; }

    /// <summary>
    /// Error message if authentication failed
    /// </summary>
    public string? Error { get; init; }
}

/// <summary>
/// Authentication methods
/// </summary>
public enum AuthMethod
{
    None,
    BrowserCookie,
    OAuth,
    ApiKey,
    CLI,
    DeviceFlow
}
