using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using DnsManager.App.Logging;
using DnsManager.Core.Logging;
using DnsManager.Core.Models;
using DnsManager.Core.Services;

using Microsoft.Win32;

using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Threading;

using Velopack;

namespace DnsManager.App.ViewModels;

/// <summary>Главная VM: адаптеры, переключение DNS (DHCP &lt;-&gt; ручной), состояние, автозапуск.</summary>
public sealed partial class MainViewModel : ObservableObject
{
	private readonly INetworkService _network;
	private readonly IDnsService _dns;
	private readonly ILogService _log;
	private readonly AutostartService _autostart;
	private readonly UiSettings _uiSettings;
	private readonly UiSettingsStore _uiSettingsStore;
	private readonly DnsScheduleService _schedule;
	private readonly DispatcherTimer _countdownTimer;
	private bool _suppressScheduleSave;
	private const string UpdateFeedUrl = "https://github.com/Dzirt089/DnsManager/releases/latest/download";

	public PresetsViewModel Presets { get; }
	public ResolutionViewModel Resolution { get; }
	public BenchmarkViewModel Benchmark { get; }
	public ObservableCollection<LogEntry> Logs { get; }

	public ObservableCollection<NetworkAdapterInfo> Adapters { get; } = [];

	[ObservableProperty]
	private NetworkAdapterInfo? _selectedAdapter;

	[ObservableProperty]
	private string _networkInfoText = "Адаптеры не загружены";

	[ObservableProperty]
	private string _dnsStateText = "—";

	[ObservableProperty]
	private bool _isBusy;

	[ObservableProperty]
	private bool _isAutostartEnabled;

	[ObservableProperty]
	private bool _isScheduledDhcpEnabled;

	[ObservableProperty]
	private string _scheduledDhcpTimeText = "";

	[ObservableProperty]
	private string _scheduledDhcpCountdownText = "Расписание выключено";

	[ObservableProperty]
	private string _statusBarText = "Готово";

	public MainViewModel(
		INetworkService network,
		IDnsService dns,
		LogService log,
		AutostartService autostart,
		UiSettings uiSettings,
		UiSettingsStore uiSettingsStore,
		DnsScheduleService schedule,
		PresetsViewModel presets,
		ResolutionViewModel resolution,
		BenchmarkViewModel benchmark)
	{
		_network = network;
		_dns = dns;
		_log = log;
		_autostart = autostart;
		_uiSettings = uiSettings;
		_uiSettingsStore = uiSettingsStore;
		_schedule = schedule;
		Presets = presets;
		Resolution = resolution;
		Benchmark = benchmark;
		Logs = log.Entries;
		IsAutostartEnabled = autostart.IsEnabled();
		_suppressScheduleSave = true;
		IsScheduledDhcpEnabled = uiSettings.ScheduledDhcpEnabled;
		ScheduledDhcpTimeText = FormatTime(uiSettings.ScheduledDhcpTimeMsk);
		_suppressScheduleSave = false;

		_countdownTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
		_countdownTimer.Tick += (_, _) => UpdateScheduleCountdown();
		_schedule.NextTriggerChanged += (_, _) => DispatchScheduleCountdownUpdate();
		_schedule.Update(uiSettings);
		UpdateScheduleCountdown();
		_countdownTimer.Start();
	}

	partial void OnIsScheduledDhcpEnabledChanged(bool value)
	{
		if (_suppressScheduleSave)
			return;
		SaveScheduleSettings();
	}

	partial void OnScheduledDhcpTimeTextChanged(string value)
	{
		if (_suppressScheduleSave)
			return;

		if (TryParseTime(value, out var time))
		{
			_uiSettings.ScheduledDhcpTimeMsk = time;
			SaveScheduleSettings();
		}
		else
		{
			UpdateScheduleCountdown();
		}
	}

	private void SaveScheduleSettings()
	{
		_uiSettings.ScheduledDhcpEnabled = IsScheduledDhcpEnabled;
		if (TryParseTime(ScheduledDhcpTimeText, out var time))
			_uiSettings.ScheduledDhcpTimeMsk = time;

		_uiSettingsStore.Save(_uiSettings);
		_schedule.Update(_uiSettings);
		_log.Info(LogEvents.DnsScheduleToggle,
			IsScheduledDhcpEnabled
				? $"Расписание автоотключения DNS включено: {FormatTime(_uiSettings.ScheduledDhcpTimeMsk)} МСК."
				: "Расписание автоотключения DNS выключено.",
			("Enabled", IsScheduledDhcpEnabled), ("TimeMsk", _uiSettings.ScheduledDhcpTimeMsk));
		UpdateScheduleCountdown();
	}

	private void UpdateScheduleCountdown()
	{
		if (!IsScheduledDhcpEnabled)
		{
			ScheduledDhcpCountdownText = "Расписание выключено";
			return;
		}

		if (!TryParseTime(ScheduledDhcpTimeText, out var time))
		{
			ScheduledDhcpCountdownText = "Некорректное время, используйте ЧЧ:ММ";
			return;
		}

		var next = _schedule.NextTriggerUtc;
		if (next is null)
		{
			ScheduledDhcpCountdownText = $"Ожидание следующего дня ({FormatTime(time)} МСК)";
			return;
		}

		var remaining = next.Value - DateTimeOffset.UtcNow;
		if (remaining <= TimeSpan.Zero)
		{
			ScheduledDhcpCountdownText = $"Ожидание следующего дня ({FormatTime(time)} МСК)";
			return;
		}

		ScheduledDhcpCountdownText = $"До отключения: {remaining:hh\\:mm\\:ss} ({FormatTime(time)} МСК)";
	}

	private void DispatchScheduleCountdownUpdate()
	{
		var dispatcher = Application.Current?.Dispatcher;
		if (dispatcher is null || dispatcher.CheckAccess())
			UpdateScheduleCountdown();
		else
			dispatcher.Invoke(UpdateScheduleCountdown);
	}

	private static string FormatTime(TimeSpan time) => $"{time.Hours:00}:{time.Minutes:00}";

	private static bool TryParseTime(string? text, out TimeSpan time)
	{
		time = default;
		if (string.IsNullOrWhiteSpace(text))
			return false;
		if (!TimeSpan.TryParseExact(text.Trim(), @"hh\:mm", null, out var parsed))
			return false;
		time = parsed;
		return true;
	}

	partial void OnSelectedAdapterChanged(NetworkAdapterInfo? value)
	{
		EnableDnsCommand.NotifyCanExecuteChanged();
		DisableDnsCommand.NotifyCanExecuteChanged();
		ApplyPresetCommand.NotifyCanExecuteChanged();
		RefreshStateCommand.NotifyCanExecuteChanged();
		UpdateNetworkInfo();
		_ = RefreshStateCommand.ExecuteAsync(null);
	}

	partial void OnIsBusyChanged(bool value)
	{
		EnableDnsCommand.NotifyCanExecuteChanged();
		DisableDnsCommand.NotifyCanExecuteChanged();
		ApplyPresetCommand.NotifyCanExecuteChanged();
		RefreshAdaptersCommand.NotifyCanExecuteChanged();
		RefreshStateCommand.NotifyCanExecuteChanged();
	}

	private bool CanOperate => SelectedAdapter is not null && !IsBusy;

	[RelayCommand]
	private async Task RefreshAdaptersAsync(CancellationToken ct)
	{
		if (IsBusy)
			return;

		IsBusy = true;
		StatusBarText = "Загрузка адаптеров...";
		_log.Info(LogEvents.AdaptersLoad, "Запрос списка сетевых адаптеров...");
		try
		{
			var adapters = await _network.GetAdaptersAsync(ct);
			Adapters.Clear();
			foreach (var adapter in adapters)
				Adapters.Add(adapter);

			SelectedAdapter = adapters.FirstOrDefault(a => a.IsActive) ?? Adapters.FirstOrDefault();
			_log.Info(LogEvents.AdaptersResult, $"Найдено адаптеров: {adapters.Count} (активных: {adapters.Count(a => a.IsActive)})",
				("Total", adapters.Count), ("Active", adapters.Count(a => a.IsActive)));
			StatusBarText = "Адаптеры загружены";
		}
		catch (Exception ex)
		{
			_log.Error(LogEvents.AdaptersResult, $"Не удалось получить адаптеры: {ex.Message}", ex);
			StatusBarText = "Ошибка загрузки адаптеров";
		}
		finally
		{
			IsBusy = false;
		}
	}

	/// <summary>Стартовая инициализация: загрузка адаптеров, автовключение DNS, если он в DHCP.</summary>
	public async Task InitializeAsync()
	{
		await RefreshAdaptersAsync(CancellationToken.None);
		await AutoEnableDnsOnStartupAsync();
	}

	private async Task AutoEnableDnsOnStartupAsync()
	{
		if (SelectedAdapter is null || !SelectedAdapter.IsActive)
		{
			_log.Warn(LogEvents.DnsStartupAutoEnable, "Активный адаптер не найден — автовключение DNS при старте пропущено.");
			return;
		}

		try
		{
			var state = await _dns.GetStateAsync(SelectedAdapter);
			if (!state.IsDhcp)
			{
				_log.Info(LogEvents.DnsStartupAutoEnable,
					"DNS уже включён — автовключение при старте пропущено.",
					("Adapter", SelectedAdapter.Name), ("InterfaceIndex", SelectedAdapter.InterfaceIndex));
				return;
			}

			var preset = Presets.DefaultPreset;
			_log.Info(LogEvents.DnsStartupAutoEnable,
				$"DNS в режиме DHCP — включаю профиль по умолчанию «{preset.Name}».",
				("Adapter", SelectedAdapter.Name), ("InterfaceIndex", SelectedAdapter.InterfaceIndex),
				("Preset", preset.Name));

			IsBusy = true;
			StatusBarText = "Автовключение DNS...";
			var ok = await _dns.EnableManualAsync(SelectedAdapter, preset);
			StatusBarText = ok ? "DNS включён автоматически при старте" : "Ошибка автовключения DNS";
			await RefreshStateCommand.ExecuteAsync(CancellationToken.None);
		}
		catch (Exception ex)
		{
			_log.Error(LogEvents.DnsStartupAutoEnable, $"Ошибка автовключения DNS при старте: {ex.Message}", ex);
		}
		finally
		{
			IsBusy = false;
		}
	}

	[RelayCommand(CanExecute = nameof(CanOperate))]
	private async Task EnableDnsAsync(CancellationToken ct)
	{
		var preset = Presets.SelectedPreset ?? DnsPreset.Default();
		_log.Info(LogEvents.DnsEnable, "Пользователь нажал «Включить DNS».",
			("Preset", preset.Name), ("Adapter", SelectedAdapter?.Name ?? ""));
		await ApplyPresetCoreAsync(preset, ct);
	}

	[RelayCommand(CanExecute = nameof(CanOperate))]
	private async Task DisableDnsAsync(CancellationToken ct)
	{
		if (SelectedAdapter is null)
			return;

		IsBusy = true;
		StatusBarText = "Возврат DNS в DHCP...";
		try
		{
			var ok = await _dns.DisableToDhcpAsync(SelectedAdapter, ct);
			StatusBarText = ok ? "DNS переключён в режим DHCP" : "Ошибка переключения DNS";
			await RefreshStateCommand.ExecuteAsync(ct);
		}
		finally
		{
			IsBusy = false;
		}
	}

	[RelayCommand(CanExecute = nameof(CanOperate))]
	private async Task ApplyPresetAsync(CancellationToken ct)
	{
		var preset = Presets.SelectedPreset ?? DnsPreset.Default();
		_log.Info(LogEvents.DnsApplyPreset, "Пользователь применил пресет.",
			("Preset", preset.Name), ("Adapter", SelectedAdapter?.Name ?? ""));
		await ApplyPresetCoreAsync(preset, ct);
	}

	private async Task ApplyPresetCoreAsync(DnsPreset preset, CancellationToken ct)
	{
		if (SelectedAdapter is null)
			return;

		IsBusy = true;
		StatusBarText = "Применение ручного DNS...";
		try
		{
			var ok = await _dns.EnableManualAsync(SelectedAdapter, preset, ct);
			StatusBarText = ok ? "Ручной DNS применён" : "Ошибка применения DNS";
			await RefreshStateCommand.ExecuteAsync(ct);
		}
		finally
		{
			IsBusy = false;
		}
	}

	[RelayCommand(CanExecute = nameof(CanOperate))]
	private async Task RefreshStateAsync(CancellationToken ct = default)
	{
		if (SelectedAdapter is null)
			return;

		try
		{
			var state = await _dns.GetStateAsync(SelectedAdapter, ct);
			DnsStateText = FormatDnsState(state);
			UpdateNetworkInfo();
		}
		catch (Exception ex)
		{
			_log.Error(LogEvents.DnsReadState, $"Не удалось прочитать состояние DNS: {ex.Message}", ex);
			DnsStateText = "Ошибка чтения";
		}
	}

	[RelayCommand]
	private void ToggleAutostart()
	{
		_autostart.SetEnabled(IsAutostartEnabled);
		_log.Info(LogEvents.AutostartToggle,
			IsAutostartEnabled ? "Автозапуск при входе в Windows включён." : "Автозапуск при входе в Windows выключен.",
			("Enabled", IsAutostartEnabled));
	}

	[RelayCommand]
	private async Task CheckForUpdateAsync()
	{
		try
		{
			var mgr = new UpdateManager(UpdateFeedUrl);
			var update = await mgr.CheckForUpdatesAsync();
			if (update is null)
			{
				StatusBarText = "Установлена актуальная версия.";
				return;
			}

			StatusBarText = "Скачивание обновления...";
			await mgr.DownloadUpdatesAsync(update);
			mgr.ApplyUpdatesAndRestart(null);   // закрывает приложение и ставит обновление
		}
		catch (Exception ex)
		{
			_log.Error("update.check", $"Не удалось проверить обновления: {ex.Message}", ex);
			StatusBarText = "Ошибка проверки обновлений";
		}
	}

	[RelayCommand]
	private void ClearLogs()
	{
		Logs.Clear();
		_log.Info(LogEvents.LogClear, "Лог очищен.");
	}

	/// <summary>Экспорт текущего лога в файл (структурный JSON или текстовый).</summary>
	[RelayCommand]
	private void SaveLogs()
	{
		var dialog = new SaveFileDialog
		{
			Title = "Сохранить лог",
			Filter = "JSON (структурный)|*.json|Текстовый файл (*.txt)|*.txt",
			FileName = $"dnsmanager-log-{DateTime.Now:yyyyMMdd-HHmmss}.json",
			AddExtension = true
		};

		if (dialog.ShowDialog() != true)
			return;

		var entries = Logs.ToList();
		try
		{
			if (dialog.FilterIndex == 2)
			{
				File.WriteAllLines(dialog.FileName,
					entries.Select(e => $"{e.DisplayTimestamp} [{e.LevelText}] {e.Message}"));
			}
			else
			{
				File.WriteAllText(dialog.FileName, LogEntrySerializer.ToJsonArray(entries));
			}

			_log.Info(LogEvents.LogExport, $"Лог сохранён в {dialog.FileName}",
				("Path", dialog.FileName), ("Entries", entries.Count));
		}
		catch (Exception ex)
		{
			_log.Error(LogEvents.LogExport, $"Не удалось сохранить лог: {ex.Message}", ex,
				("Path", dialog.FileName));
		}
	}

	private void UpdateNetworkInfo()
	{
		var a = SelectedAdapter;
		if (a is null)
		{
			NetworkInfoText = "Адаптер не выбран";
			return;
		}

		var sb = new StringBuilder();
		sb.Append($"Тип: {a.NetworkType} • Статус: {a.Status} • Скорость: {a.LinkSpeed}");
		if (a.HasProfile)
			sb.Append($"\nПодключение: {a.ConnectionName} • Сеть: {a.NetworkCategory} • IPv4: {a.IPv4Connectivity}");
		else
			sb.Append("\nПодключение отсутствует (нет профиля)");
		NetworkInfoText = sb.ToString();
	}

	private static string FormatDnsState(DnsState state)
	{
		if (state.IsDhcp)
			return "Автоматически (DHCP)";

		var parts = state.Servers.Select(s =>
		{
			var doh = s.DohEnabled
				? $"DoH вкл ({(s.AutoUpgrade ? "авто-шаблон" : "вручную")}), fallback {(s.AllowFallbackToUdp ? "вкл" : "откл")}"
				: "DoH выкл";
			return $"{s.Address} ({doh})";
		});
		return string.Join("; ", parts);
	}
}
