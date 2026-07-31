using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DnsManager.Core.Logging;
using DnsManager.Core.Services;

namespace DnsManager.App.ViewModels;

/// <summary>Бенчмарк DNS-серверов: латентность прямых запросов к серверу.</summary>
public sealed partial class BenchmarkViewModel : ObservableObject
{
    private readonly DnsBenchmarkService _benchmark;
    private readonly ILogService _log;

    public ObservableCollection<BenchmarkResult> Results { get; } = [];

    [ObservableProperty]
    private string _serversText = "111.88.96.50\n111.88.96.51\n8.8.8.8\n1.1.1.1\n77.88.8.8";

    [ObservableProperty]
    private bool _isRunning;

    [ObservableProperty]
    private string _statusText = "";

    public BenchmarkViewModel(DnsBenchmarkService benchmark, ILogService log)
    {
        _benchmark = benchmark;
        _log = log;
    }

    [RelayCommand]
    private async Task RunBenchmarkAsync(CancellationToken ct)
    {
        if (IsRunning)
            return;

        IsRunning = true;
        Results.Clear();
        StatusText = "Запуск...";
        try
        {
            var servers = ServersText.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            _log.Info(LogEvents.BenchmarkRun, $"Запуск бенчмарка ({servers.Length} серверов)", ("Servers", servers.Length));
            foreach (var server in servers)
            {
                StatusText = $"Тестирую {server}...";
                try
                {
                    var result = await _benchmark.BenchmarkAsync(server, DnsBenchmarkService.DefaultDomains, 3, ct);
                    Results.Add(result);
                    _log.Info(LogEvents.BenchmarkResult,
                        $"Бенчмарк {server}: успешно {result.SuccessfulQueries}/{result.TotalQueries}, avg {result.AvgMs} мс, потери {result.LossPercent:F1}%",
                        ("Server", server), ("Success", result.SuccessfulQueries),
                        ("Total", result.TotalQueries), ("AvgMs", result.AvgMs),
                        ("LossPercent", result.LossPercent));
                }
                catch (Exception ex)
                {
                    _log.Error(LogEvents.BenchmarkResult, $"Бенчмарк {server} — ошибка: {ex.Message}", ex,
                        ("Server", server));
                }
            }

            // Сортировка по средней задержке (лучшие сверху).
            var sorted = Results.OrderBy(r => r.AvgMs).ToList();
            Results.Clear();
            foreach (var item in sorted)
                Results.Add(item);
            StatusText = "Готово";
        }
        finally
        {
            IsRunning = false;
        }
    }
}
