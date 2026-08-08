namespace DnsManager.Core.Models;

/// <summary>Текущее состояние DNS-сервера на адаптере.</summary>
public sealed record DnsServerState
{
	public string Address { get; init; } = "";

	public bool DohEnabled { get; init; }

	public bool AutoUpgrade { get; init; }

	public bool AllowFallbackToUdp { get; init; } = true;

	public string? DohTemplate { get; init; }
}

/// <summary>Текущее состояние DNS на интерфейсе.</summary>
public sealed record DnsState
{
	public string InterfaceAlias { get; init; } = "";

	public bool IsDhcp { get; init; }

	public IReadOnlyList<DnsServerState> Servers { get; init; } = [];
}
