using DnsManager.Core.Logging;
using DnsManager.Core.Models;
using DnsManager.Core.PowerShell;

using System.Diagnostics;
using System.Text.Json;

namespace DnsManager.Core.Services;

/// <summary>Реализация IDnsService через PowerShell (Set-DnsClientServerAddress / Set-DnsClientDohServerAddress).</summary>
public sealed class DnsService : IDnsService
{
	private readonly IPowerShellRunner _runner;
	private readonly ILogService _log;

	public DnsService(IPowerShellRunner runner, ILogService log)
	{
		_runner = runner;
		_log = log;
	}

	public async Task<bool> EnableManualAsync(NetworkAdapterInfo adapter, DnsPreset preset, CancellationToken ct = default)
	{
		var script = PowerShellCommandBuilder.EnableManualScript(adapter.InterfaceIndex, preset);
		var result = await RunLoggedAsync(script, LogEvents.DnsEnable,
			$"Включение ручного DNS ({preset.Name}) на «{adapter.Name}»", ct,
			("Adapter", adapter.Name), ("InterfaceIndex", adapter.InterfaceIndex),
			("Preset", preset.Name), ("Servers", preset.Servers.Count));
		return result.IsSuccess;
	}

	public async Task<bool> DisableToDhcpAsync(NetworkAdapterInfo adapter, CancellationToken ct = default)
	{
		var script = PowerShellCommandBuilder.DisableToDhcpScript(adapter.InterfaceIndex);
		var result = await RunLoggedAsync(script, LogEvents.DnsDisable,
			$"Возврат DNS в режим DHCP (автоматически) на «{adapter.Name}»", ct,
			("Adapter", adapter.Name), ("InterfaceIndex", adapter.InterfaceIndex));
		return result.IsSuccess;
	}

	public async Task<DnsState> GetStateAsync(NetworkAdapterInfo adapter, CancellationToken ct = default)
	{
		var serversJson = (await _runner.RunAsync(PowerShellCommandBuilder.GetDnsServersScript(adapter.InterfaceIndex), ct)).StdOut;
		var dohJson = (await _runner.RunAsync(PowerShellCommandBuilder.GetDohServersScript(adapter.InterfaceIndex), ct)).StdOut;
		var staticJson = (await _runner.RunAsync(PowerShellCommandBuilder.GetStaticDnsScript(adapter.InterfaceIndex), ct)).StdOut;

		var servers = ParseDnsServers(serversJson);
		var dohByAddress = ParseDohServers(dohJson).ToDictionary(d => d.Address);
		var isDhcp = !ParseStaticDns(staticJson);

		var states = servers
			.Select(addr => dohByAddress.TryGetValue(addr, out var doh)
				? new DnsServerState
				{
					Address = addr,
					DohEnabled = true,
					AutoUpgrade = doh.AutoUpgrade,
					AllowFallbackToUdp = doh.AllowFallbackToUdp,
					DohTemplate = doh.DohTemplate
				}
				: new DnsServerState { Address = addr, DohEnabled = false })
			.ToList();

		_log.Info(LogEvents.DnsReadState,
			$"Состояние DNS на «{adapter.Name}»: {(isDhcp ? "DHCP" : string.Join(", ", states.Select(s => s.Address)))}",
			("Adapter", adapter.Name), ("InterfaceIndex", adapter.InterfaceIndex),
			("Dhcp", isDhcp), ("Servers", states.Count));

		return new DnsState
		{
			InterfaceAlias = adapter.Name,
			IsDhcp = isDhcp,
			Servers = states
		};
	}

	/// <summary>Реестровый NameServer: пуст при DHCP, содержит IP при статической настройке.</summary>
	internal static bool ParseStaticDns(string json)
	{
		try
		{
			using var doc = JsonDocument.Parse(json);
			return doc.RootElement.TryGetProperty("StaticDns", out var el) &&
				   el.ValueKind == JsonValueKind.True;
		}
		catch (JsonException)
		{
			return false;
		}
	}

	private async Task<PowerShellResult> RunLoggedAsync(string script, string eventName, string action,
		CancellationToken ct, params (string Key, object? Value)[] baseProps)
	{
		var sw = Stopwatch.StartNew();
		_log.Info(eventName, $"{action}...", baseProps);

		PowerShellResult result;
		try
		{
			result = await _runner.RunAsync(script, ct);
		}
		catch (Exception ex)
		{
			sw.Stop();
			_log.Error(eventName, $"{action} — ошибка выполнения: {ex.Message}", ex,
				("DurationMs", sw.ElapsedMilliseconds));
			return new PowerShellResult(-1, "", ex.Message);
		}

		sw.Stop();
		var props = baseProps.Concat([("ExitCode", result.ExitCode), ("DurationMs", sw.ElapsedMilliseconds)]).ToArray();
		_log.Debug(LogEvents.App, $"Команда: {script}");
		if (result.IsSuccess)
			_log.Info(eventName, $"{action} — успешно. Вывод: {Trim(result.StdOut)}", props);
		else
			_log.Error(eventName, $"{action} — ОШИБКА (exit {result.ExitCode}). {Trim(result.StdErr)}", null, props);
		return result;
	}

	private static string Trim(string s) => s.Replace("\r", " ").Replace("\n", " ").Trim();

	internal static List<string> ParseDnsServers(string json)
	{
		if (string.IsNullOrWhiteSpace(json)) return [];
		try
		{
			using var doc = JsonDocument.Parse(json);
			var root = doc.RootElement;
			var items = root.ValueKind == JsonValueKind.Array ? root.EnumerateArray().ToList() : [root.Clone()];
			var result = new List<string>();
			foreach (var item in items)
			{
				if (item.ValueKind != JsonValueKind.Object)
					continue;
				if (!item.TryGetProperty("ServerAddresses", out var sa) || sa.ValueKind != JsonValueKind.Array)
					continue;
				foreach (var addr in sa.EnumerateArray())
				{
					if (addr.ValueKind == JsonValueKind.String && !string.IsNullOrEmpty(addr.GetString()))
						result.Add(addr.GetString()!);
				}
			}
			return result.Distinct().ToList();
		}
		catch (JsonException)
		{
			return [];
		}
	}

	/// <summary>DoH-серверы интерфейса из DohInterfaceSettings\Doh: адрес, шаблон, DohFlags (1 = авто-шаблон).</summary>
	internal static List<DnsServerState> ParseDohServers(string json)
	{
		if (string.IsNullOrWhiteSpace(json)) return [];
		try
		{
			using var doc = JsonDocument.Parse(json);
			var root = doc.RootElement;
			var items = root.ValueKind == JsonValueKind.Array ? root.EnumerateArray().ToList() : [root.Clone()];
			var result = new List<DnsServerState>();
			foreach (var item in items)
			{
				if (item.ValueKind != JsonValueKind.Object)
					continue;
				var flags = GetInt(item, "DohFlags");
				result.Add(new DnsServerState
				{
					Address = GetStr(item, "ServerAddress"),
					DohEnabled = true,
					AutoUpgrade = flags == 1,
					AllowFallbackToUdp = true,
					DohTemplate = GetStrOrNull(item, "DohTemplate")
				});
			}
			return result;
		}
		catch (JsonException)
		{
			return [];
		}
	}

	private static int GetInt(JsonElement item, string prop) =>
		item.TryGetProperty(prop, out var el) && el.ValueKind == JsonValueKind.Number ? el.GetInt32() : 0;

	private static string GetStr(JsonElement item, string prop) =>
		item.TryGetProperty(prop, out var el) && el.ValueKind == JsonValueKind.String ? el.GetString() ?? "" : "";

	private static string? GetStrOrNull(JsonElement item, string prop) =>
		item.TryGetProperty(prop, out var el) && el.ValueKind == JsonValueKind.String && !string.IsNullOrEmpty(el.GetString())
			? el.GetString()
			: null;

	private static bool GetBool(JsonElement item, string prop, bool def = false) =>
		item.TryGetProperty(prop, out var el) &&
		(el.ValueKind == JsonValueKind.True || el.ValueKind == JsonValueKind.False)
			? el.GetBoolean()
			: def;
}
