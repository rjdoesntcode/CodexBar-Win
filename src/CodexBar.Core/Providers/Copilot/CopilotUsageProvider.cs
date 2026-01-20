using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using CodexBar.Core.Models;

namespace CodexBar.Core.Providers.Copilot;

/// <summary>
/// Provider for GitHub Copilot usage data
/// </summary>
public class CopilotUsageProvider : IUsageProvider
{
    private readonly HttpClient _httpClient;
    private string? _cachedToken;

    public ProviderType ProviderType => ProviderType.Copilot;
    public string DisplayName => "GitHub Copilot";

    private const string GitHubApiBase = "https://api.github.com";

    public CopilotUsageProvider(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "CodexBar/1.0");
        _httpClient.DefaultRequestHeaders.Add("Accept", "application/vnd.github+json");
    }

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        var token = await GetGitHubTokenAsync(cancellationToken);
        return token != null;
    }

    public async Task<UsageSnapshot> FetchUsageAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var token = await GetGitHubTokenAsync(cancellationToken);
            if (token == null)
            {
                return new UsageSnapshot
                {
                    Provider = ProviderType.Copilot,
                    Error = "GitHub Copilot is not configured. Sign in with GitHub CLI (gh auth login)."
                };
            }

            // Get user info
            var user = await GetUserInfoAsync(token, cancellationToken);

            // Get Copilot subscription status
            var copilotStatus = await GetCopilotStatusAsync(token, cancellationToken);

            return new UsageSnapshot
            {
                Provider = ProviderType.Copilot,
                UserIdentity = user?.Login ?? user?.Email,
                PlanName = copilotStatus?.Plan ?? "Unknown",
                Primary = copilotStatus?.IsActive == true ? new RateWindow
                {
                    Name = "Active",
                    UsagePercent = 0, // Copilot doesn't have usage limits like other providers
                    ResetsAt = copilotStatus?.SeatManagementSetting != null ? null : copilotStatus?.SeatExpiresAt,
                    WindowDuration = null
                } : null
            };
        }
        catch (Exception ex)
        {
            return new UsageSnapshot
            {
                Provider = ProviderType.Copilot,
                Error = $"Failed to fetch Copilot status: {ex.Message}"
            };
        }
    }

    public async Task<AuthStatus> GetAuthStatusAsync(CancellationToken cancellationToken = default)
    {
        var token = await GetGitHubTokenAsync(cancellationToken);
        if (token == null)
        {
            return new AuthStatus
            {
                IsAuthenticated = false,
                Method = AuthMethod.None,
                Error = "Not authenticated with GitHub"
            };
        }

        try
        {
            var user = await GetUserInfoAsync(token, cancellationToken);
            return new AuthStatus
            {
                IsAuthenticated = true,
                Method = AuthMethod.CLI,
                UserIdentity = user?.Login ?? user?.Email ?? "GitHub User"
            };
        }
        catch
        {
            return new AuthStatus
            {
                IsAuthenticated = true,
                Method = AuthMethod.CLI,
                UserIdentity = "GitHub User"
            };
        }
    }

    private async Task<string?> GetGitHubTokenAsync(CancellationToken cancellationToken)
    {
        if (_cachedToken != null)
            return _cachedToken;

        // Try to get token from gh CLI
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "gh",
                    Arguments = "auth token",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);

            if (process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output))
            {
                _cachedToken = output.Trim();
                return _cachedToken;
            }
        }
        catch
        {
            // gh CLI not available
        }

        // Try to read from credential helper or environment
        var envToken = Environment.GetEnvironmentVariable("GITHUB_TOKEN")
            ?? Environment.GetEnvironmentVariable("GH_TOKEN");

        if (!string.IsNullOrEmpty(envToken))
        {
            _cachedToken = envToken;
            return _cachedToken;
        }

        return null;
    }

    private async Task<GitHubUser?> GetUserInfoAsync(string token, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{GitHubApiBase}/user");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
            return null;

        return await response.Content.ReadFromJsonAsync<GitHubUser>(cancellationToken: cancellationToken);
    }

    private async Task<CopilotStatus?> GetCopilotStatusAsync(string token, CancellationToken cancellationToken)
    {
        // Try to get user's Copilot subscription
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{GitHubApiBase}/user/copilot_billing/subscriptions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _httpClient.SendAsync(request, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            var subscriptions = await response.Content.ReadFromJsonAsync<CopilotSubscriptionsResponse>(cancellationToken: cancellationToken);
            if (subscriptions?.Subscriptions?.Count > 0)
            {
                var sub = subscriptions.Subscriptions[0];
                return new CopilotStatus
                {
                    IsActive = true,
                    Plan = sub.Plan?.Name ?? "Copilot",
                    SeatExpiresAt = sub.ExpiresAt
                };
            }
        }

        // Check if user has Copilot access via organization
        using var orgRequest = new HttpRequestMessage(HttpMethod.Get, $"{GitHubApiBase}/user/orgs");
        orgRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var orgResponse = await _httpClient.SendAsync(orgRequest, cancellationToken);
        if (orgResponse.IsSuccessStatusCode)
        {
            var orgs = await orgResponse.Content.ReadFromJsonAsync<List<GitHubOrg>>(cancellationToken: cancellationToken);
            // If user is in any org, they might have Copilot access
            if (orgs?.Count > 0)
            {
                return new CopilotStatus
                {
                    IsActive = true,
                    Plan = "Organization",
                    SeatManagementSetting = "org"
                };
            }
        }

        // Default to checking if Copilot extension is available
        return new CopilotStatus
        {
            IsActive = false,
            Plan = "Not subscribed"
        };
    }
}

// JSON response models
internal record GitHubUser
{
    [JsonPropertyName("login")]
    public string? Login { get; init; }

    [JsonPropertyName("email")]
    public string? Email { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }
}

internal record GitHubOrg
{
    [JsonPropertyName("login")]
    public string? Login { get; init; }
}

internal record CopilotSubscriptionsResponse
{
    [JsonPropertyName("subscriptions")]
    public List<CopilotSubscription>? Subscriptions { get; init; }
}

internal record CopilotSubscription
{
    [JsonPropertyName("plan")]
    public CopilotPlan? Plan { get; init; }

    [JsonPropertyName("expires_at")]
    public DateTime? ExpiresAt { get; init; }
}

internal record CopilotPlan
{
    [JsonPropertyName("name")]
    public string? Name { get; init; }
}

internal record CopilotStatus
{
    public bool IsActive { get; init; }
    public string? Plan { get; init; }
    public DateTime? SeatExpiresAt { get; init; }
    public string? SeatManagementSetting { get; init; }
}
