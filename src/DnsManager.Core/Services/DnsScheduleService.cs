using DnsManager.Core.Logging;
using DnsManager.Core.Models;

namespace DnsManager.Core.Services;

/// <summary>
/// Ежедневное расписание автоотключения DNS: в заданное время МСК, если DNS включён вручную,
/// переключает активный адаптер в DHCP. Если время уже прошло — планирует следующий день.
/// </summary>
public sealed class DnsScheduleService : IDisposable
{
	private readonly IDnsService _dns;
	private readonly INetworkService _network;
	private readonly ILogService _log;
	private readonly object _gate = new();

	private Timer? _timer;
	private UiSettings _settings = new();
	private DateTimeOffset? _nextTriggerUtc;
	private int _isRunning;

	public DnsScheduleService(IDnsService dns, INetworkService network, ILogService log)
	{
		_dns = dns;
		_network = network;
		_log = log;
	}

	/// <summary>Ближайший момент срабатывания в UTC; null — расписание выключено.</summary>
	public DateTimeOffset? NextTriggerUtc
	{
		get
		{
			lock (_gate)
				return _nextTriggerUtc;
		}
	}

	/// <summary>Событие при изменении ближайшего срабатывания (включение/выключение/смена времени).</summary>
	public event EventHandler? NextTriggerChanged;

	/// <summary>Пересоздаёт расписание по текущим настройкам.</summary>
	public void Update(UiSettings settings)
	{
		ArgumentNullException.ThrowIfNull(settings);

		lock (_gate)
		{
			_settings = settings;
			_timer?.Dispose();
			_timer = null;
			_nextTriggerUtc = null;

			if (!settings.ScheduledDhcpEnabled)
				return;

			var next = MskScheduleCalculator.NextUtc(settings.ScheduledDhcpTimeMsk, DateTime.UtcNow);
			var delay = next - DateTimeOffset.UtcNow;
			if (delay < TimeSpan.Zero)
				delay = TimeSpan.Zero;

			_nextTriggerUtc = next;
			_timer = new Timer(_ => _ = ExecuteScheduledSwitchAsync(), null, delay, Timeout.InfiniteTimeSpan);
		}

		NextTriggerChanged?.Invoke(this, EventArgs.Empty);
	}

	/// <summary>Выполняет переключение в DHCP и планирует следующий день. Публичный для тестов.</summary>
	public async Task ExecuteScheduledSwitchAsync()
	{
		if (Interlocked.Exchange(ref _isRunning, 1) != 0)
			return;

		try
		{
			_log.Info(LogEvents.DnsScheduleTrigger,
				"Сработало расписание автоотключения DNS...",
				("ScheduledTimeMsk", _settings.ScheduledDhcpTimeMsk));

			var adapters = await _network.GetAdaptersAsync();
			var adapter = adapters.FirstOrDefault(a => a.IsActive);
			if (adapter is null)
			{
				_log.Warn(LogEvents.DnsScheduleTrigger, "Нет активного адаптера — переключение в DHCP пропущено.");
				return;
			}

			var state = await _dns.GetStateAsync(adapter);
			if (state.IsDhcp)
			{
				_log.Info(LogEvents.DnsScheduleTrigger,
					$"DNS на «{adapter.Name}» уже в режиме DHCP — переключение пропущено.",
					("Adapter", adapter.Name), ("InterfaceIndex", adapter.InterfaceIndex));
				return;
			}

			var ok = await _dns.DisableToDhcpAsync(adapter);
			_log.Info(LogEvents.DnsScheduleTrigger,
				ok
					? $"DNS переключён в DHCP по расписанию на «{adapter.Name}»."
					: $"Ошибка переключения DNS в DHCP по расписанию на «{adapter.Name}».",
				("Adapter", adapter.Name), ("InterfaceIndex", adapter.InterfaceIndex), ("Ok", ok));
		}
		catch (Exception ex)
		{
			_log.Error(LogEvents.DnsScheduleTrigger,
				$"Ошибка выполнения расписания автоотключения DNS: {ex.Message}", ex);
		}
		finally
		{
			Interlocked.Exchange(ref _isRunning, 0);
			Update(_settings);
		}
	}

	public void Dispose()
	{
		lock (_gate)
		{
			_timer?.Dispose();
			_timer = null;
		}
	}
}
