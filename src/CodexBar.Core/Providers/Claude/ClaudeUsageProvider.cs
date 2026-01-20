using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CodexBar.Core.Browser;
using CodexBar.Core.Models;

namespace CodexBar.Core.Providers.Claude;

/// <summary>
/// Provider for Claude AI usage data
/// </summary>
public class ClaudeUsageProvider : IUsageProvider
{
    private readonly BrowserCookieService _cookieService;
    private readonly HttpClient _httpClient;
    private string? _cachedCliPath;

    public ProviderType ProviderType => ProviderType.Claude;
    public string DisplayName => "Claude";

    private static readonly string[] ClaudeCookieNames =
    [
        "sessionKey",
        "__cf_bm",
        "__cflb"
    ];

    public ClaudeUsageProvider(BrowserCookieService cookieService, HttpClient? httpClient = null)
    {
        _cookieService = cookieService;
        _httpClient = httpClient ?? new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "CodexBar/1.0");
    }

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        // Check if Claude CLI is available
        var cliPath = await FindClaudeCliPathAsync(cancellationToken);
        if (cliPath != null)
            return true;

        // Check if browser cookies are available
        var cookie = await _cookieService.GetCookieAsync("claude.ai", "sessionKey", cancellationToken);
        return cookie != null;
    }

    public async Task<UsageSnapshot> FetchUsageAsync(CancellationToken cancellationToken = default)
    {
        // Try CLI first (most reliable)
        var cliResult = await TryFetchFromCliAsync(cancellationToken);
        if (cliResult != null)
            return cliResult;

        // Fall back to web API with cookies
        var webResult = await TryFetchFromWebAsync(cancellationToken);
        if (webResult != null)
            return webResult;

        return new UsageSnapshot
        {
            Provider = ProviderType.Claude,
            Error = "Claude is not configured. Install Claude CLI or sign in to claude.ai in your browser."
        };
    }

    public async Task<AuthStatus> GetAuthStatusAsync(CancellationToken cancellationToken = default)
    {
        var cliPath = await FindClaudeCliPathAsync(cancellationToken);
        if (cliPath != null)
        {
            return new AuthStatus
            {
                IsAuthenticated = true,
                Method = AuthMethod.CLI,
                UserIdentity = "CLI User"
            };
        }

        var cookie = await _cookieService.GetCookieAsync("claude.ai", "sessionKey", cancellationToken);
        if (cookie != null)
        {
            return new AuthStatus
            {
                IsAuthenticated = true,
                Method = AuthMethod.BrowserCookie,
                UserIdentity = "Browser Session"
            };
        }

        return new AuthStatus
        {
            IsAuthenticated = false,
            Method = AuthMethod.None,
            Error = "Not authenticated"
        };
    }

    private async Task<UsageSnapshot?> TryFetchFromCliAsync(CancellationToken cancellationToken)
    {
        try
        {
            var cliPath = await FindClaudeCliPathAsync(cancellationToken);
            if (cliPath == null)
                return null;

            // Run claude --version to check if it's working
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = cliPath,
                    Arguments = "usage --json",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);

            if (process.ExitCode != 0)
                return null;

            // Parse the JSON output
            var usage = JsonSerializer.Deserialize<ClaudeCliUsageResponse>(output);
            if (usage == null)
                return null;

            return ConvertCliResponseToSnapshot(usage);
        }
        catch
        {
            return null;
        }
    }

    private async Task<UsageSnapshot?> TryFetchFromWebAsync(CancellationToken cancellationToken)
    {
        try
        {
            var cookieHeader = await _cookieService.GetCookieHeaderAsync("claude.ai", ClaudeCookieNames, cancellationToken);
            if (cookieHeader == null)
                return null;

            using var request = new HttpRequestMessage(HttpMethod.Get, "https://claude.ai/api/organizations");
            request.Headers.Add("Cookie", cookieHeader);

            var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return null;

            var orgs = await response.Content.ReadFromJsonAsync<List<ClaudeOrganization>>(cancellationToken: cancellationToken);
            if (orgs == null || orgs.Count == 0)
                return null;

            var primaryOrg = orgs.First();

            // Fetch usage for the primary organization
            using var usageRequest = new HttpRequestMessage(HttpMethod.Get,
                $"https://claude.ai/api/organizations/{primaryOrg.Uuid}/usage");
            usageRequest.Headers.Add("Cookie", cookieHeader);

            var usageResponse = await _httpClient.SendAsync(usageRequest, cancellationToken);
            if (!usageResponse.IsSuccessStatusCode)
            {
                // Return basic info even without detailed usage
                return new UsageSnapshot
                {
                    Provider = ProviderType.Claude,
                    UserIdentity = primaryOrg.Name,
                    PlanName = primaryOrg.RateLimitTier
                };
            }

            var usage = await usageResponse.Content.ReadFromJsonAsync<ClaudeWebUsageResponse>(cancellationToken: cancellationToken);
            return ConvertWebResponseToSnapshot(usage, primaryOrg);
        }
        catch (Exception ex)
        {
            return new UsageSnapshot
            {
                Provider = ProviderType.Claude,
                Error = $"Failed to fetch Claude usage: {ex.Message}"
            };
        }
    }

    private async Task<string?> FindClaudeCliPathAsync(CancellationToken cancellationToken)
    {
        if (_cachedCliPath != null && File.Exists(_cachedCliPath))
            return _cachedCliPath;

        // Common installation paths on Windows
        var possiblePaths = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "claude", "claude.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Claude", "claude.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "bin", "claude.exe"),
            "claude.exe" // In PATH
        };

        foreach (var path in possiblePaths)
        {
            if (path == "claude.exe")
            {
                // Check if claude is in PATH
                try
                {
                    var process = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = "where",
                            Arguments = "claude",
                            RedirectStandardOutput = true,
                            UseShellExecute = false,
                            CreateNoWindow = true
                        }
                    };
                    process.Start();
                    var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
                    await process.WaitForExitAsync(cancellationToken);

                    if (process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output))
                    {
                        _cachedCliPath = output.Split('\n')[0].Trim();
                        return _cachedCliPath;
                    }
                }
                catch
                {
                    continue;
                }
            }
            else if (File.Exists(path))
            {
                _cachedCliPath = path;
                return _cachedCliPath;
            }
        }

        return null;
    }

    private UsageSnapshot ConvertCliResponseToSnapshot(ClaudeCliUsageResponse response)
    {
        var windows = new List<RateWindow>();

        if (response.SessionWindow != null)
        {
            windows.Add(new RateWindow
            {
                Name = "Session",
                UsagePercent = response.SessionWindow.UsagePercent,
                ResetsAt = response.SessionWindow.ResetsAt,
                WindowDuration = TimeSpan.FromHours(5)
            });
        }

        if (response.WeeklyWindow != null)
        {
            windows.Add(new RateWindow
            {
                Name = "Weekly",
                UsagePercent = response.WeeklyWindow.UsagePercent,
                ResetsAt = response.WeeklyWindow.ResetsAt,
                WindowDuration = TimeSpan.FromDays(7)
            });
        }

        return new UsageSnapshot
        {
            Provider = ProviderType.Claude,
            Primary = windows.FirstOrDefault(w => w.Name == "Session"),
            Secondary = windows.FirstOrDefault(w => w.Name == "Weekly"),
            Additional = windows.Where(w => w.Name != "Session" && w.Name != "Weekly").ToList(),
            UserIdentity = response.Email,
            PlanName = response.Plan
        };
    }

    private UsageSnapshot ConvertWebResponseToSnapshot(ClaudeWebUsageResponse? usage, ClaudeOrganization org)
    {
        if (usage == null)
        {
            return new UsageSnapshot
            {
                Provider = ProviderType.Claude,
                UserIdentity = org.Name,
                PlanName = org.RateLimitTier
            };
        }

        RateWindow? primary = null;
        RateWindow? secondary = null;

        if (usage.DailyUsage != null)
        {
            primary = new RateWindow
            {
                Name = "Session",
                UsagePercent = usage.DailyUsage.Limit > 0
                    ? (double)usage.DailyUsage.Used / usage.DailyUsage.Limit
                    : 0,
                ResetsAt = usage.DailyUsage.ResetsAt,
                WindowDuration = TimeSpan.FromHours(5)
            };
        }

        if (usage.MonthlyUsage != null)
        {
            secondary = new RateWindow
            {
                Name = "Monthly",
                UsagePercent = usage.MonthlyUsage.Limit > 0
                    ? (double)usage.MonthlyUsage.Used / usage.MonthlyUsage.Limit
                    : 0,
                ResetsAt = usage.MonthlyUsage.ResetsAt,
                WindowDuration = TimeSpan.FromDays(30)
            };
        }

        return new UsageSnapshot
        {
            Provider = ProviderType.Claude,
            Primary = primary,
            Secondary = secondary,
            UserIdentity = org.Name,
            PlanName = org.RateLimitTier
        };
    }
}

// JSON response models
internal record ClaudeCliUsageResponse
{
    [JsonPropertyName("email")]
    public string? Email { get; init; }

    [JsonPropertyName("plan")]
    public string? Plan { get; init; }

    [JsonPropertyName("session_window")]
    public ClaudeRateWindowResponse? SessionWindow { get; init; }

    [JsonPropertyName("weekly_window")]
    public ClaudeRateWindowResponse? WeeklyWindow { get; init; }
}

internal record ClaudeRateWindowResponse
{
    [JsonPropertyName("usage_percent")]
    public double UsagePercent { get; init; }

    [JsonPropertyName("resets_at")]
    public DateTime? ResetsAt { get; init; }
}

internal record ClaudeOrganization
{
    [JsonPropertyName("uuid")]
    public string Uuid { get; init; } = "";

    [JsonPropertyName("name")]
    public string Name { get; init; } = "";

    [JsonPropertyName("rate_limit_tier")]
    public string? RateLimitTier { get; init; }
}

internal record ClaudeWebUsageResponse
{
    [JsonPropertyName("daily_usage")]
    public ClaudeUsageWindow? DailyUsage { get; init; }

    [JsonPropertyName("monthly_usage")]
    public ClaudeUsageWindow? MonthlyUsage { get; init; }
}

internal record ClaudeUsageWindow
{
    [JsonPropertyName("used")]
    public long Used { get; init; }

    [JsonPropertyName("limit")]
    public long Limit { get; init; }

    [JsonPropertyName("resets_at")]
    public DateTime? ResetsAt { get; init; }
}
