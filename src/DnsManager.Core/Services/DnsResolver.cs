using System.Diagnostics;
using System.Net;

namespace DnsManager.Core.Services;

/// <summary>Результат разрешения одного домена.</summary>
public sealed record ResolutionResult(string Host, bool Success, IReadOnlyList<string> Addresses, long ElapsedMs, string? Error = null)
{
	public string AddressesText => string.Join(", ", Addresses);
	public string StatusText => Success ? "OK" : "Ошибка";
}

/// <summary>Тест резолвинга доменов через системный DNS (текущие настройки адаптера).</summary>
public sealed class DnsResolver
{
	public async Task<ResolutionResult> ResolveAsync(string host, CancellationToken ct = default)
	{
		var sw = Stopwatch.StartNew();
		try
		{
			var addresses = await Dns.GetHostAddressesAsync(host, ct);
			sw.Stop();
			return new ResolutionResult(host, addresses.Length > 0,
				addresses.Select(a => a.ToString()).ToArray(), sw.ElapsedMilliseconds);
		}
		catch (Exception ex)
		{
			sw.Stop();
			return new ResolutionResult(host, false, [], sw.ElapsedMilliseconds, ex.Message);
		}
	}
}
