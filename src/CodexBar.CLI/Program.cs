using System.CommandLine;
using System.Text.Json;
using CodexBar.Core.Browser;
using CodexBar.Core.Models;
using CodexBar.Core.Providers;

// Create the root command
var rootCommand = new RootCommand("CodexBar CLI - AI Usage Monitor")
{
    Name = "codexbar"
};

// Status command
var statusCommand = new Command("status", "Show current usage status for all providers");
var jsonOption = new Option<bool>("--json", "Output in JSON format");
statusCommand.AddOption(jsonOption);

statusCommand.SetHandler(async (bool json) =>
{
    var settings = await AppSettings.LoadAsync();
    var cookieService = new BrowserCookieService(settings.PreferredBrowser);
    var providerManager = new ProviderManager(cookieService);

    Console.WriteLine("Fetching usage data...\n");

    await providerManager.RefreshAllAsync(settings);
    var statuses = providerManager.GetAllStatuses();

    if (json)
    {
        var output = statuses.ToDictionary(
            kvp => kvp.Key.ToString(),
            kvp => new
            {
                Provider = kvp.Key.ToString(),
                kvp.Value.Level,
                Summary = kvp.Value.GetSummary(),
                kvp.Value.UserIdentity,
                kvp.Value.Usage?.PlanName,
                Primary = kvp.Value.Usage?.Primary != null ? new
                {
                    kvp.Value.Usage.Primary.Name,
                    UsagePercent = Math.Round(kvp.Value.Usage.Primary.UsagePercent * 100, 1),
                    kvp.Value.Usage.Primary.TimeUntilReset
                } : null,
                Secondary = kvp.Value.Usage?.Secondary != null ? new
                {
                    kvp.Value.Usage.Secondary.Name,
                    UsagePercent = Math.Round(kvp.Value.Usage.Secondary.UsagePercent * 100, 1),
                    kvp.Value.Usage.Secondary.TimeUntilReset
                } : null,
                Error = kvp.Value.ErrorMessage ?? kvp.Value.Usage?.Error
            });

        var jsonOutput = JsonSerializer.Serialize(output, new JsonSerializerOptions { WriteIndented = true });
        Console.WriteLine(jsonOutput);
    }
    else
    {
        foreach (var (provider, status) in statuses)
        {
            if (!settings.IsProviderEnabled(provider))
                continue;

            var statusIcon = status.Level switch
            {
                ProviderStatusLevel.Healthy => "[OK]",
                ProviderStatusLevel.Warning => "[!!]",
                ProviderStatusLevel.Critical => "[XX]",
                ProviderStatusLevel.Error => "[ER]",
                _ => "[??]"
            };

            Console.WriteLine($"{statusIcon} {provider.GetDisplayName()}");
            Console.WriteLine($"    Status: {status.GetSummary()}");

            if (status.Usage?.UserIdentity != null)
                Console.WriteLine($"    User: {status.Usage.UserIdentity}");

            if (status.Usage?.PlanName != null)
                Console.WriteLine($"    Plan: {status.Usage.PlanName}");

            if (status.Usage?.Primary != null)
            {
                var p = status.Usage.Primary;
                Console.WriteLine($"    {p.Name}: {p.UsagePercent * 100:F1}% (resets in {p.TimeUntilReset})");
            }

            if (status.Usage?.Secondary != null)
            {
                var s = status.Usage.Secondary;
                Console.WriteLine($"    {s.Name}: {s.UsagePercent * 100:F1}% (resets in {s.TimeUntilReset})");
            }

            if (status.ErrorMessage != null)
                Console.WriteLine($"    Error: {status.ErrorMessage}");

            Console.WriteLine();
        }
    }
}, jsonOption);

rootCommand.AddCommand(statusCommand);

// Providers command
var providersCommand = new Command("providers", "List available providers");
providersCommand.SetHandler(() =>
{
    Console.WriteLine("Available Providers:");
    Console.WriteLine();

    foreach (ProviderType provider in Enum.GetValues<ProviderType>())
    {
        Console.WriteLine($"  {provider.GetDisplayName()}");
        Console.WriteLine($"    Type: {provider}");
        Console.WriteLine($"    Website: {provider.GetWebsiteUrl()}");
        Console.WriteLine();
    }
});

rootCommand.AddCommand(providersCommand);

// Config command
var configCommand = new Command("config", "Show or modify configuration");
var showOption = new Option<bool>("--show", "Show current configuration");
configCommand.AddOption(showOption);

configCommand.SetHandler(async (bool show) =>
{
    var settings = await AppSettings.LoadAsync();

    if (show || true) // Always show for now
    {
        Console.WriteLine("Current Configuration:");
        Console.WriteLine();
        Console.WriteLine($"  Refresh Interval: {settings.RefreshIntervalMinutes} minutes");
        Console.WriteLine($"  Preferred Browser: {settings.PreferredBrowser}");
        Console.WriteLine($"  Warning Notifications: {settings.ShowWarningNotifications}");
        Console.WriteLine($"  Exceeded Notifications: {settings.ShowExceededNotifications}");
        Console.WriteLine($"  Start with Windows: {settings.StartWithWindows}");
        Console.WriteLine();
        Console.WriteLine("Enabled Providers:");

        foreach (var (provider, enabled) in settings.EnabledProviders)
        {
            Console.WriteLine($"    [{(enabled ? "X" : " ")}] {provider.GetDisplayName()}");
        }
    }
}, showOption);

rootCommand.AddCommand(configCommand);

// Run the CLI
return await rootCommand.InvokeAsync(args);
