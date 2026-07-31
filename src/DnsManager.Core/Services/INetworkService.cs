using DnsManager.Core.Models;

namespace DnsManager.Core.Services;

/// <summary>Определение сетевых адаптеров и типа подключения.</summary>
public interface INetworkService
{
    Task<IReadOnlyList<NetworkAdapterInfo>> GetAdaptersAsync(CancellationToken ct = default);
}
