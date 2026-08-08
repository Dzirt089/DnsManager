namespace DnsManager.Core.Models;

/// <summary>Сетевой адаптер Windows (объединение Get-NetAdapter + Get-NetConnectionProfile).</summary>
public sealed record NetworkAdapterInfo
{
	/// <summary>InterfaceAlias, напр. «Wi-Fi» или «Ethernet».</summary>
	public string Name { get; init; } = "";

	public int InterfaceIndex { get; init; }

	public string Description { get; init; } = "";

	/// <summary>Status адаптера: Up / Down / Disconnected / Not Present.</summary>
	public string Status { get; init; } = "";

	/// <summary>Скорость линка, напр. «450 Mbps» или «1 Gbps».</summary>
	public string LinkSpeed { get; init; } = "";

	/// <summary>MediaType из Get-NetAdapter (802.11, Ethernet, Bluetooth и т.д.).</summary>
	public string MediaType { get; init; } = "";

	/// <summary>Имя профиля подключения (из Get-NetConnectionProfile), null если нет.</summary>
	public string? ConnectionName { get; init; }

	/// <summary>NetworkCategory: Public / Private / DomainAuthenticated.</summary>
	public string NetworkCategory { get; init; } = "";

	/// <summary>IPv4Connectivity: Internet / NoTraffic / LocalNetwork и т.д.</summary>
	public string IPv4Connectivity { get; init; } = "";

	/// <summary>Тип сети, вычисляется из MediaType/Description: Wi-Fi, Ethernet, Bluetooth, Mobile и т.д.</summary>
	public string NetworkType => ClassifyNetworkType();

	/// <summary>Адаптер активен: поднят и имеет профиль подключения.</summary>
	public bool IsActive => HasProfile && Status.Equals("Up", StringComparison.OrdinalIgnoreCase);

	public bool HasProfile => ConnectionName is not null;

	private string ClassifyNetworkType()
	{
		var haystack = $"{MediaType} {Description}".ToLowerInvariant();
		if (haystack.Contains("802.11") || haystack.Contains("wlan") || haystack.Contains("wi-fi"))
			return "Wi-Fi";
		if (haystack.Contains("ethernet") || haystack.Contains("gigabit") || haystack.Contains("wired"))
			return "Ethernet";
		if (haystack.Contains("bluetooth"))
			return "Bluetooth";
		if (haystack.Contains("wwan") || haystack.Contains("mobile") || haystack.Contains("lte"))
			return "Мобильный интернет";
		return "Другое";
	}
}
