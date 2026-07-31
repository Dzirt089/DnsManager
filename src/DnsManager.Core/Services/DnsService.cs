using System.Text.Json;
using DnsManager.Core.Logging;
using DnsManager.Core.Models;
using DnsManager.Core.PowerShell;

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
        var result = await RunLoggedAsync(script, $"Включение ручного DNS ({preset.Name}) на «{adapter.Name}»", ct);
        return result.IsSuccess;
    }

    public async Task<bool> DisableToDhcpAsync(NetworkAdapterInfo adapter, CancellationToken ct = default)
    {
        var script = PowerShellCommandBuilder.DisableToDhcpScript(adapter.InterfaceIndex);
        var result = await RunLoggedAsync(script, $"Возврат DNS в режим DHCP (автоматически) на «{adapter.Name}»", ct);
        return result.IsSuccess;
    }

    public async Task<DnsState> GetStateAsync(NetworkAdapterInfo adapter, CancellationToken ct = default)
    {
        var serversJson = (await _runner.RunAsync(PowerShellCommandBuilder.GetDnsServersScript(adapter.InterfaceIndex), ct)).StdOut;
        var dohJson = (await _runner.RunAsync(PowerShellCommandBuilder.GetDohServersScript(adapter.InterfaceIndex), ct)).StdOut;

        var servers = ParseDnsServers(serversJson);
        var dohByAddress = ParseDohServers(dohJson).ToDictionary(d => d.Address);

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

        return new DnsState
        {
            InterfaceAlias = adapter.Name,
            IsDhcp = states.Count == 0,
            Servers = states
        };
    }

    private async Task<PowerShellResult> RunLoggedAsync(string script, string action, CancellationToken ct)
    {
        _log.Info($"{action}...");
        PowerShellResult result;
        try
        {
            result = await _runner.RunAsync(script, ct);
        }
        catch (Exception ex)
        {
            _log.Error($"{action} — ошибка выполнения: {ex.Message}", ex);
            return new PowerShellResult(-1, "", ex.Message);
        }

        _log.Debug($"Команда: {script}");
        if (result.IsSuccess)
            _log.Info($"{action} — успешно. Вывод: {Trim(result.StdOut)}");
        else
            _log.Error($"{action} — ОШИБКА (exit {result.ExitCode}). {Trim(result.StdErr)}");
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
                result.Add(new DnsServerState
                {
                    Address = GetStr(item, "ServerAddress"),
                    DohEnabled = true,
                    AutoUpgrade = GetBool(item, "AutoUpgrade"),
                    AllowFallbackToUdp = GetBool(item, "AllowFallbackToUdp", true),
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
