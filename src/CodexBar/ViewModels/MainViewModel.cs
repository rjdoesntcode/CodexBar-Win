using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CodexBar.Core.Models;
using CodexBar.Core.Providers;

namespace CodexBar.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly ProviderManager _providerManager;
    private readonly AppSettings _settings;

    [ObservableProperty]
    private bool _isRefreshing;

    [ObservableProperty]
    private string _statusText = "Loading...";

    public MainViewModel(ProviderManager providerManager, AppSettings settings)
    {
        _providerManager = providerManager;
        _settings = settings;

        _providerManager.AllRefreshed += (_, e) => UpdateStatusText(e.Statuses);
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (IsRefreshing) return;

        IsRefreshing = true;
        try
        {
            await _providerManager.RefreshAllAsync(_settings);
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    private void UpdateStatusText(IReadOnlyDictionary<ProviderType, ProviderStatus> statuses)
    {
        var lines = new List<string>();

        foreach (var (provider, status) in statuses)
        {
            if (_settings.IsProviderEnabled(provider))
            {
                lines.Add($"{provider.GetDisplayName()}: {status.GetSummary()}");
            }
        }

        StatusText = lines.Count > 0
            ? string.Join("\n", lines)
            : "No providers enabled";
    }
}
