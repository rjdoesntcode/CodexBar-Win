using CodexBar.Core.Models;

namespace CodexBar.Core.Browser;

/// <summary>
/// Interface for reading cookies from browsers
/// </summary>
public interface IBrowserCookieReader
{
    /// <summary>
    /// The browser type this reader handles
    /// </summary>
    BrowserType BrowserType { get; }

    /// <summary>
    /// Whether this browser is installed
    /// </summary>
    bool IsInstalled { get; }

    /// <summary>
    /// Gets cookies for a specific domain
    /// </summary>
    Task<IReadOnlyList<Cookie>> GetCookiesAsync(string domain, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a specific cookie by name and domain
    /// </summary>
    Task<Cookie?> GetCookieAsync(string domain, string name, CancellationToken cancellationToken = default);
}
