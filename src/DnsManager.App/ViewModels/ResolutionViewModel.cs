using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DnsManager.Core.Logging;
using DnsManager.Core.Services;

namespace DnsManager.App.ViewModels;

/// <summary>Тест резолвинга доменов через системный DNS.</summary>
public sealed partial class ResolutionViewModel : ObservableObject
{
    private readonly DnsResolver _resolver;
    private readonly ILogService _log;

    public ObservableCollection<ResolutionResult> Results { get; } = [];

    [ObservableProperty]
    private string _domainsText = "ya.ru\ngoogle.com\nvk.com\ndns.google\ngithub.com";

    [ObservableProperty]
    private bool _isRunning;

    public ResolutionViewModel(DnsResolver resolver, ILogService log)
    {
        _resolver = resolver;
        _log = log;
    }

    [RelayCommand]
    private async Task RunAsync(CancellationToken ct)
    {
        if (IsRunning)
            return;

        IsRunning = true;
        Results.Clear();
        try
        {
            var hosts = DomainsText.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            _log.Info(LogEvents.ResolveRun, $"Запуск теста резолвинга ({hosts.Length} доменов)", ("Hosts", hosts.Length));
            foreach (var host in hosts)
            {
                var result = await _resolver.ResolveAsync(host, ct);
                Results.Add(result);
                _log.Info(LogEvents.ResolveResult,
                    result.Success
                        ? $"{host} -> {string.Join(", ", result.Addresses)} ({result.ElapsedMs} мс)"
                        : $"{host} — не разрешён: {result.Error} ({result.ElapsedMs} мс)",
                    ("Host", host), ("Success", result.Success),
                    ("DurationMs", result.ElapsedMs), ("Addresses", result.AddressesText));
            }
        }
        finally
        {
            IsRunning = false;
        }
    }
}
