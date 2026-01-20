using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using CodexBar.Core.Models;

namespace CodexBar.Core.Providers.Codex;

/// <summary>
/// Provider for OpenAI Codex (codex CLI) usage data
/// </summary>
public class CodexUsageProvider : IUsageProvider
{
    private string? _cachedCliPath;

    public ProviderType ProviderType => ProviderType.Codex;
    public string DisplayName => "Codex";

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        var cliPath = await FindCodexCliPathAsync(cancellationToken);
        return cliPath != null;
    }

    public async Task<UsageSnapshot> FetchUsageAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var cliPath = await FindCodexCliPathAsync(cancellationToken);
            if (cliPath == null)
            {
                return new UsageSnapshot
                {
                    Provider = ProviderType.Codex,
                    Error = "Codex CLI is not installed. Install it from https://github.com/openai/codex"
                };
            }

            // Try to get usage via RPC first
            var rpcResult = await TryFetchViaRpcAsync(cancellationToken);
            if (rpcResult != null)
                return rpcResult;

            // Fall back to CLI command
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

            if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(output))
            {
                return new UsageSnapshot
                {
                    Provider = ProviderType.Codex,
                    Error = "Failed to fetch usage from Codex CLI"
                };
            }

            var usage = JsonSerializer.Deserialize<CodexUsageResponse>(output);
            return ConvertToSnapshot(usage);
        }
        catch (Exception ex)
        {
            return new UsageSnapshot
            {
                Provider = ProviderType.Codex,
                Error = $"Failed to fetch Codex usage: {ex.Message}"
            };
        }
    }

    public async Task<AuthStatus> GetAuthStatusAsync(CancellationToken cancellationToken = default)
    {
        var cliPath = await FindCodexCliPathAsync(cancellationToken);
        if (cliPath == null)
        {
            return new AuthStatus
            {
                IsAuthenticated = false,
                Method = AuthMethod.None,
                Error = "Codex CLI not installed"
            };
        }

        try
        {
            // Check if authenticated by trying to get whoami
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = cliPath,
                    Arguments = "whoami --json",
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
                var whoami = JsonSerializer.Deserialize<CodexWhoamiResponse>(output);
                return new AuthStatus
                {
                    IsAuthenticated = true,
                    Method = AuthMethod.CLI,
                    UserIdentity = whoami?.Email ?? "Codex User"
                };
            }
        }
        catch
        {
            // Fall through
        }

        return new AuthStatus
        {
            IsAuthenticated = false,
            Method = AuthMethod.None,
            Error = "Not authenticated with Codex CLI"
        };
    }

    private async Task<UsageSnapshot?> TryFetchViaRpcAsync(CancellationToken cancellationToken)
    {
        // Codex uses JSON-RPC over a local socket
        // For now, fall back to CLI - RPC implementation can be added later
        await Task.CompletedTask;
        return null;
    }

    private async Task<string?> FindCodexCliPathAsync(CancellationToken cancellationToken)
    {
        if (_cachedCliPath != null && File.Exists(_cachedCliPath))
            return _cachedCliPath;

        // Common installation paths on Windows
        var possiblePaths = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "codex", "codex.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Codex", "codex.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codex", "bin", "codex.exe"),
            "codex.exe" // In PATH
        };

        foreach (var path in possiblePaths)
        {
            if (path == "codex.exe")
            {
                try
                {
                    var process = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = "where",
                            Arguments = "codex",
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

    private UsageSnapshot ConvertToSnapshot(CodexUsageResponse? usage)
    {
        if (usage == null)
        {
            return new UsageSnapshot
            {
                Provider = ProviderType.Codex,
                Error = "Invalid usage response"
            };
        }

        RateWindow? primary = null;
        RateWindow? secondary = null;

        if (usage.RateLimits != null)
        {
            if (usage.RateLimits.Session != null)
            {
                primary = new RateWindow
                {
                    Name = "Session",
                    UsagePercent = usage.RateLimits.Session.UsagePercent,
                    ResetsAt = usage.RateLimits.Session.ResetsAt,
                    WindowDuration = TimeSpan.FromHours(3)
                };
            }

            if (usage.RateLimits.Daily != null)
            {
                secondary = new RateWindow
                {
                    Name = "Daily",
                    UsagePercent = usage.RateLimits.Daily.UsagePercent,
                    ResetsAt = usage.RateLimits.Daily.ResetsAt,
                    WindowDuration = TimeSpan.FromDays(1)
                };
            }
        }

        return new UsageSnapshot
        {
            Provider = ProviderType.Codex,
            Primary = primary,
            Secondary = secondary,
            UserIdentity = usage.Email,
            PlanName = usage.Plan
        };
    }
}

// JSON response models
internal record CodexUsageResponse
{
    [JsonPropertyName("email")]
    public string? Email { get; init; }

    [JsonPropertyName("plan")]
    public string? Plan { get; init; }

    [JsonPropertyName("rate_limits")]
    public CodexRateLimits? RateLimits { get; init; }
}

internal record CodexRateLimits
{
    [JsonPropertyName("session")]
    public CodexRateWindow? Session { get; init; }

    [JsonPropertyName("daily")]
    public CodexRateWindow? Daily { get; init; }
}

internal record CodexRateWindow
{
    [JsonPropertyName("usage_percent")]
    public double UsagePercent { get; init; }

    [JsonPropertyName("resets_at")]
    public DateTime? ResetsAt { get; init; }
}

internal record CodexWhoamiResponse
{
    [JsonPropertyName("email")]
    public string? Email { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }
}
