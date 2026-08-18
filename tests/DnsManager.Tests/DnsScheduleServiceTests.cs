using DnsManager.Core.Logging;
using DnsManager.Core.Models;
using DnsManager.Core.Services;

namespace DnsManager.Tests;

public class DnsScheduleServiceTests
{
	[Fact]
	public async Task Execute_WhenManualDns_DisablesToDhcp()
	{
		var dns = new FakeDnsService { State = new DnsState { IsDhcp = false } };
		var network = new FakeNetworkService
		{
			Adapters = [ActiveAdapter()]
		};
		using var service = new DnsScheduleService(dns, network, new FakeLogService());
		service.Update(new UiSettings { ScheduledDhcpEnabled = true, ScheduledDhcpTimeMsk = new TimeSpan(16, 55, 0) });

		await service.ExecuteScheduledSwitchAsync();

		Assert.Equal(1, dns.DisableCalls);
	}

	[Fact]
	public async Task Execute_WhenAlreadyDhcp_Skips()
	{
		var dns = new FakeDnsService { State = new DnsState { IsDhcp = true } };
		var network = new FakeNetworkService
		{
			Adapters = [ActiveAdapter()]
		};
		using var service = new DnsScheduleService(dns, network, new FakeLogService());
		service.Update(new UiSettings { ScheduledDhcpEnabled = true, ScheduledDhcpTimeMsk = new TimeSpan(16, 55, 0) });

		await service.ExecuteScheduledSwitchAsync();

		Assert.Equal(0, dns.DisableCalls);
	}

	[Fact]
	public async Task Execute_WhenNoActiveAdapter_Skips()
	{
		var dns = new FakeDnsService { State = new DnsState { IsDhcp = false } };
		var network = new FakeNetworkService
		{
			Adapters =
			[
				new NetworkAdapterInfo { Name = "Ethernet", InterfaceIndex = 2, Status = "Disconnected" }
			]
		};
		using var service = new DnsScheduleService(dns, network, new FakeLogService());
		service.Update(new UiSettings { ScheduledDhcpEnabled = true, ScheduledDhcpTimeMsk = new TimeSpan(16, 55, 0) });

		await service.ExecuteScheduledSwitchAsync();

		Assert.Equal(0, dns.DisableCalls);
	}

	private static NetworkAdapterInfo ActiveAdapter() =>
		new()
		{
			Name = "Wi-Fi",
			InterfaceIndex = 1,
			Status = "Up",
			ConnectionName = "Test Network"
		};

	private sealed class FakeNetworkService : INetworkService
	{
		public IReadOnlyList<NetworkAdapterInfo> Adapters { get; set; } = [];

		public Task<IReadOnlyList<NetworkAdapterInfo>> GetAdaptersAsync(CancellationToken ct = default)
			=> Task.FromResult(Adapters);
	}

	private sealed class FakeDnsService : IDnsService
	{
		public DnsState State { get; set; } = new() { IsDhcp = true };

		public int DisableCalls { get; private set; }

		public Task<bool> EnableManualAsync(NetworkAdapterInfo adapter, DnsPreset preset, CancellationToken ct = default)
			=> Task.FromResult(true);

		public Task<bool> DisableToDhcpAsync(NetworkAdapterInfo adapter, CancellationToken ct = default)
		{
			DisableCalls++;
			return Task.FromResult(true);
		}

		public Task<DnsState> GetStateAsync(NetworkAdapterInfo adapter, CancellationToken ct = default)
			=> Task.FromResult(State);
	}

	private sealed class FakeLogService : ILogService
	{
		public void Log(LogLevel level, string eventName, string message,
			IReadOnlyDictionary<string, object?>? properties = null, Exception? exception = null)
		{
		}

		public void Info(string message)
		{
		}

		public void Info(string eventName, string message, params (string Key, object? Value)[] properties)
		{
		}

		public void Warn(string message)
		{
		}

		public void Warn(string eventName, string message, params (string Key, object? Value)[] properties)
		{
		}

		public void Error(string message, Exception? exception = null)
		{
		}

		public void Error(string eventName, string message, Exception? exception = null,
			params (string Key, object? Value)[] properties)
		{
		}

		public void Debug(string message)
		{
		}

		public void Debug(string eventName, string message, params (string Key, object? Value)[] properties)
		{
		}
	}
}
