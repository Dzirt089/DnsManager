using DnsManager.Core.Models;
using DnsManager.Core.PowerShell;

using System.Text.Json;

namespace DnsManager.Core.Services;

/// <summary>Получение адаптеров и профилей подключений через PowerShell, объединение по InterfaceIndex.</summary>
public sealed class NetworkService : INetworkService
{
	private readonly IPowerShellRunner _runner;

	public NetworkService(IPowerShellRunner runner) => _runner = runner;

	public async Task<IReadOnlyList<NetworkAdapterInfo>> GetAdaptersAsync(CancellationToken ct = default)
	{
		var adaptersJson = (await _runner.RunAsync(PowerShellCommandBuilder.GetAdaptersScript(), ct)).StdOut;
		var profilesJson = (await _runner.RunAsync(PowerShellCommandBuilder.GetProfilesScript(), ct)).StdOut;

		var adapters = ParseAdapters(adaptersJson);
		var profiles = ParseProfiles(profilesJson);
		var profilesByIndex = profiles.ToDictionary(p => p.InterfaceIndex);

		return adapters
			.Select(a => profilesByIndex.TryGetValue(a.InterfaceIndex, out var p)
				? a with
				{
					ConnectionName = p.Name,
					NetworkCategory = p.NetworkCategory,
					IPv4Connectivity = p.IPv4Connectivity
				}
				: a)
			.ToList();
	}

	internal static List<NetworkAdapterInfo> ParseAdapters(string json)
	{
		if (string.IsNullOrWhiteSpace(json)) return [];

		var items = DeserializeArray(json);
		var result = new List<NetworkAdapterInfo>();
		foreach (var item in items)
		{
			if (item.ValueKind != JsonValueKind.Object)
				continue;
			result.Add(new NetworkAdapterInfo
			{
				Name = Str(item, "Name"),
				Description = Str(item, "InterfaceDescription"),
				InterfaceIndex = Int(item, "InterfaceIndex"),
				Status = Str(item, "Status"),
				LinkSpeed = Str(item, "LinkSpeed"),
				MediaType = Str(item, "MediaType")
			});
		}
		return result;
	}

	internal static List<NetworkAdapterInfo> ParseProfiles(string json)
	{
		if (string.IsNullOrWhiteSpace(json)) return [];

		var items = DeserializeArray(json);
		var result = new List<NetworkAdapterInfo>();
		foreach (var item in items)
		{
			if (item.ValueKind != JsonValueKind.Object)
				continue;
			result.Add(new NetworkAdapterInfo
			{
				Name = Str(item, "InterfaceAlias"),
				InterfaceIndex = Int(item, "InterfaceIndex"),
				ConnectionName = StrOrNull(item, "Name"),
				NetworkCategory = Str(item, "NetworkCategory"),
				IPv4Connectivity = Str(item, "IPv4Connectivity")
			});
		}
		return result;
	}

	private static List<JsonElement> DeserializeArray(string json)
	{
		// ConvertTo-Json: один объект -> без массива; несколько -> массив.
		using var doc = JsonDocument.Parse(json);
		var root = doc.RootElement;
		if (root.ValueKind == JsonValueKind.Array)
			return root.EnumerateArray().Select(e => e.Clone()).ToList();
		if (root.ValueKind == JsonValueKind.Object)
			return [root.Clone()];
		return [];
	}

	private static string Str(JsonElement item, string prop) =>
		item.TryGetProperty(prop, out var el) && el.ValueKind == JsonValueKind.String ? el.GetString() ?? "" : "";

	private static string? StrOrNull(JsonElement item, string prop) =>
		item.TryGetProperty(prop, out var el) && el.ValueKind == JsonValueKind.String && !string.IsNullOrEmpty(el.GetString())
			? el.GetString()
			: null;

	private static int Int(JsonElement item, string prop) =>
		item.TryGetProperty(prop, out var el) && el.ValueKind == JsonValueKind.Number ? el.GetInt32() : 0;
}
