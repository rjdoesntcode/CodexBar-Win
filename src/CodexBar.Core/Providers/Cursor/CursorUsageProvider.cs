using System.Net.Http.Json;
using System.Text.Json.Serialization;
using CodexBar.Core.Browser;
using CodexBar.Core.Models;

namespace CodexBar.Core.Providers.Cursor;

/// <summary>
/// Provider for Cursor AI usage data
/// </summary>
public class CursorUsageProvider : IUsageProvider
{
    private readonly BrowserCookieService _cookieService;
    private readonly HttpClient _httpClient;

    public ProviderType ProviderType => ProviderType.Cursor;
    public string DisplayName => "Cursor";

    private const string CursorApiBase = "https://www.cursor.com";

    private static readonly string[] CursorCookieNames =
    [
        "WorkosCursorSessionToken",
        "__Secure-next-auth.session-token",
        "next-auth.session-token"
    ];

    private static readonly string[] CursorDomains = ["cursor.com", "cursor.sh"];

    public CursorUsageProvider(BrowserCookieService cookieService, HttpClient? httpClient = null)
    {
        _cookieService = cookieService;
        _httpClient = httpClient ?? new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "CodexBar/1.0");
    }

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        foreach (var domain in CursorDomains)
        {
            var cookie = await _cookieService.GetCookieAsync(domain, CursorCookieNames[0], cancellationToken);
            if (cookie != null)
                return true;

            cookie = await _cookieService.GetCookieAsync(domain, CursorCookieNames[1], cancellationToken);
            if (cookie != null)
                return true;
        }

        return false;
    }

    public async Task<UsageSnapshot> FetchUsageAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var cookieHeader = await GetCookieHeaderAsync(cancellationToken);
            if (cookieHeader == null)
            {
                return new UsageSnapshot
                {
                    Provider = ProviderType.Cursor,
                    Error = "Cursor is not configured. Sign in to cursor.com in your browser."
                };
            }

            // Fetch usage summary
            using var usageRequest = new HttpRequestMessage(HttpMethod.Get, $"{CursorApiBase}/api/usage");
            usageRequest.Headers.Add("Cookie", cookieHeader);

            var usageResponse = await _httpClient.SendAsync(usageRequest, cancellationToken);

            CursorUsageResponse? usage = null;
            if (usageResponse.IsSuccessStatusCode)
            {
                usage = await usageResponse.Content.ReadFromJsonAsync<CursorUsageResponse>(cancellationToken: cancellationToken);
            }

            // Fetch user info
            using var userRequest = new HttpRequestMessage(HttpMethod.Get, $"{CursorApiBase}/api/auth/me");
            userRequest.Headers.Add("Cookie", cookieHeader);

            var userResponse = await _httpClient.SendAsync(userRequest, cancellationToken);
            CursorUserResponse? user = null;
            if (userResponse.IsSuccessStatusCode)
            {
                user = await userResponse.Content.ReadFromJsonAsync<CursorUserResponse>(cancellationToken: cancellationToken);
            }

            return ConvertToSnapshot(usage, user);
        }
        catch (Exception ex)
        {
            return new UsageSnapshot
            {
                Provider = ProviderType.Cursor,
                Error = $"Failed to fetch Cursor usage: {ex.Message}"
            };
        }
    }

    public async Task<AuthStatus> GetAuthStatusAsync(CancellationToken cancellationToken = default)
    {
        var cookieHeader = await GetCookieHeaderAsync(cancellationToken);
        if (cookieHeader == null)
        {
            return new AuthStatus
            {
                IsAuthenticated = false,
                Method = AuthMethod.None,
                Error = "Not authenticated"
            };
        }

        try
        {
            using var userRequest = new HttpRequestMessage(HttpMethod.Get, $"{CursorApiBase}/api/auth/me");
            userRequest.Headers.Add("Cookie", cookieHeader);

            var userResponse = await _httpClient.SendAsync(userRequest, cancellationToken);
            if (userResponse.IsSuccessStatusCode)
            {
                var user = await userResponse.Content.ReadFromJsonAsync<CursorUserResponse>(cancellationToken: cancellationToken);
                return new AuthStatus
                {
                    IsAuthenticated = true,
                    Method = AuthMethod.BrowserCookie,
                    UserIdentity = user?.Email ?? "Cursor User"
                };
            }
        }
        catch
        {
            // Fall through
        }

        return new AuthStatus
        {
            IsAuthenticated = true,
            Method = AuthMethod.BrowserCookie,
            UserIdentity = "Browser Session"
        };
    }

    private async Task<string?> GetCookieHeaderAsync(CancellationToken cancellationToken)
    {
        foreach (var domain in CursorDomains)
        {
            var header = await _cookieService.GetCookieHeaderAsync(domain, CursorCookieNames, cancellationToken);
            if (header != null)
                return header;
        }

        return null;
    }

    private UsageSnapshot ConvertToSnapshot(CursorUsageResponse? usage, CursorUserResponse? user)
    {
        RateWindow? primary = null;
        RateWindow? secondary = null;

        if (usage != null)
        {
            // Premium requests (fast requests)
            if (usage.NumRequests.HasValue || usage.MaxRequestUsage.HasValue)
            {
                var used = usage.NumRequests ?? 0;
                var limit = usage.MaxRequestUsage ?? 500;
                primary = new RateWindow
                {
                    Name = "Premium",
                    UsagePercent = limit > 0 ? (double)used / limit : 0,
                    ResetsAt = usage.BillingPeriodEnd,
                    WindowDuration = TimeSpan.FromDays(30)
                };
            }

            // Slow requests (if on free plan or quota exceeded)
            if (usage.NumSlowRequests.HasValue)
            {
                secondary = new RateWindow
                {
                    Name = "Slow",
                    UsagePercent = 0, // Slow requests typically unlimited
                    ResetsAt = null,
                    WindowDuration = null
                };
            }
        }

        return new UsageSnapshot
        {
            Provider = ProviderType.Cursor,
            Primary = primary,
            Secondary = secondary,
            UserIdentity = user?.Email,
            PlanName = usage?.Subscription ?? user?.Subscription ?? "Free"
        };
    }
}

// JSON response models
internal record CursorUsageResponse
{
    [JsonPropertyName("numRequests")]
    public int? NumRequests { get; init; }

    [JsonPropertyName("numSlowRequests")]
    public int? NumSlowRequests { get; init; }

    [JsonPropertyName("maxRequestUsage")]
    public int? MaxRequestUsage { get; init; }

    [JsonPropertyName("subscription")]
    public string? Subscription { get; init; }

    [JsonPropertyName("billingPeriodStart")]
    public DateTime? BillingPeriodStart { get; init; }

    [JsonPropertyName("billingPeriodEnd")]
    public DateTime? BillingPeriodEnd { get; init; }
}

internal record CursorUserResponse
{
    [JsonPropertyName("email")]
    public string? Email { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("subscription")]
    public string? Subscription { get; init; }

    [JsonPropertyName("id")]
    public string? Id { get; init; }
}
