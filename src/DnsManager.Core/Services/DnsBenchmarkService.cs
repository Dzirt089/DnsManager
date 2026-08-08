using DnsClient;

using System.Diagnostics;
using System.Net;

namespace DnsManager.Core.Services;

/// <summary>Результат бенчмарка одного DNS-сервера.</summary>
public sealed record BenchmarkResult(
	string Server,
	int TotalQueries,
	int SuccessfulQueries,
	long MinMs,
	long AvgMs,
	long MaxMs)
{
	public int LostQueries => TotalQueries - SuccessfulQueries;
	public double LossPercent => TotalQueries == 0 ? 0 : (double)LostQueries / TotalQueries * 100;
}

/// <summary>Бенчмарк DNS-серверов: измеряет латентность прямых запросов к серверу (DnsClient).</summary>
public sealed class DnsBenchmarkService
{
	/// <summary>Домены для запросов по умолчанию.</summary>
	public static readonly string[] DefaultDomains = ["ya.ru", "google.com", "dns.google", "vk.com", "github.com"];

	public async Task<BenchmarkResult> BenchmarkAsync(
		string serverAddress,
		IReadOnlyList<string> domains,
		int queriesPerDomain = 3,
		CancellationToken ct = default)
	{
		if (!IPAddress.TryParse(serverAddress, out var ip))
			throw new ArgumentException($"Некорректный IP-адрес сервера: {serverAddress}");

		var client = new LookupClient(new IPEndPoint(ip, 53));
		var times = new List<long>();
		var total = 0;
		var success = 0;

		foreach (var domain in domains)
		{
			for (var i = 0; i < queriesPerDomain; i++)
			{
				ct.ThrowIfCancellationRequested();
				var sw = Stopwatch.StartNew();
				try
				{
					var response = await client.QueryAsync(domain, QueryType.A);
					sw.Stop();
					total++;
					if (response.HasError || response.Answers.Count == 0)
						continue;
					success++;
					times.Add(sw.ElapsedMilliseconds);
				}
				catch
				{
					sw.Stop();
					total++;
				}
			}
		}

		return new BenchmarkResult(
			serverAddress,
			total,
			success,
			times.Count > 0 ? times.Min() : 0,
			times.Count > 0 ? (long)times.Average() : 0,
			times.Count > 0 ? times.Max() : 0);
	}
}
