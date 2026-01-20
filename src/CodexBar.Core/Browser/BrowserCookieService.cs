using CodexBar.Core.Models;

namespace CodexBar.Core.Browser;

/// <summary>
/// Service for reading cookies from multiple browsers
/// </summary>
public class BrowserCookieService
{
    private readonly Dictionary<BrowserType, IBrowserCookieReader> _readers;
    private readonly List<BrowserType> _browserOrder;

    public BrowserCookieService(BrowserType preferredBrowser = BrowserType.Chrome)
    {
        _readers = new Dictionary<BrowserType, IBrowserCookieReader>
        {
            [BrowserType.Chrome] = new ChromiumCookieReader(BrowserType.Chrome),
            [BrowserType.Edge] = new ChromiumCookieReader(BrowserType.Edge),
            [BrowserType.Brave] = new ChromiumCookieReader(BrowserType.Brave),
            [BrowserType.Opera] = new ChromiumCookieReader(BrowserType.Opera),
            [BrowserType.Firefox] = new FirefoxCookieReader()
        };

        // Set browser priority order based on preferred browser
        _browserOrder = new List<BrowserType> { preferredBrowser };
        foreach (BrowserType browser in Enum.GetValues<BrowserType>())
        {
            if (browser != preferredBrowser)
                _browserOrder.Add(browser);
        }
    }

    /// <summary>
    /// Gets the list of installed browsers
    /// </summary>
    public IReadOnlyList<BrowserType> GetInstalledBrowsers()
    {
        return _readers
            .Where(kvp => kvp.Value.IsInstalled)
            .Select(kvp => kvp.Key)
            .ToList();
    }

    /// <summary>
    /// Gets cookies for a domain from the preferred browser, falling back to others if needed
    /// </summary>
    public async Task<IReadOnlyList<Cookie>> GetCookiesAsync(
        string domain,
        CancellationToken cancellationToken = default)
    {
        foreach (var browserType in _browserOrder)
        {
            if (!_readers.TryGetValue(browserType, out var reader))
                continue;

            if (!reader.IsInstalled)
                continue;

            try
            {
                var cookies = await reader.GetCookiesAsync(domain, cancellationToken);
                if (cookies.Count > 0)
                    return cookies;
            }
            catch
            {
                // Try next browser
            }
        }

        return [];
    }

    /// <summary>
    /// Gets a specific cookie from any available browser
    /// </summary>
    public async Task<Cookie?> GetCookieAsync(
        string domain,
        string name,
        CancellationToken cancellationToken = default)
    {
        foreach (var browserType in _browserOrder)
        {
            if (!_readers.TryGetValue(browserType, out var reader))
                continue;

            if (!reader.IsInstalled)
                continue;

            try
            {
                var cookie = await reader.GetCookieAsync(domain, name, cancellationToken);
                if (cookie != null)
                    return cookie;
            }
            catch
            {
                // Try next browser
            }
        }

        return null;
    }

    /// <summary>
    /// Gets cookies from a specific browser
    /// </summary>
    public async Task<IReadOnlyList<Cookie>> GetCookiesFromBrowserAsync(
        BrowserType browserType,
        string domain,
        CancellationToken cancellationToken = default)
    {
        if (!_readers.TryGetValue(browserType, out var reader))
            return [];

        if (!reader.IsInstalled)
            return [];

        return await reader.GetCookiesAsync(domain, cancellationToken);
    }

    /// <summary>
    /// Formats cookies as a Cookie header value
    /// </summary>
    public static string FormatCookieHeader(IEnumerable<Cookie> cookies)
    {
        return string.Join("; ", cookies.Where(c => !c.IsExpired).Select(c => c.ToString()));
    }

    /// <summary>
    /// Gets specific cookies by name and formats as a header
    /// </summary>
    public async Task<string?> GetCookieHeaderAsync(
        string domain,
        IEnumerable<string> cookieNames,
        CancellationToken cancellationToken = default)
    {
        var cookies = await GetCookiesAsync(domain, cancellationToken);
        var nameSet = cookieNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var matchedCookies = cookies.Where(c => nameSet.Contains(c.Name) && !c.IsExpired).ToList();

        if (matchedCookies.Count == 0)
            return null;

        return FormatCookieHeader(matchedCookies);
    }
}
