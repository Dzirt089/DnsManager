using DnsManager.Core.Models;

namespace DnsManager.Core.Services;

/// <summary>Переключение DNS адаптера между DHCP и ручным профилем, чтение текущего состояния.</summary>
public interface IDnsService
{
    Task<bool> EnableManualAsync(NetworkAdapterInfo adapter, DnsPreset preset, CancellationToken ct = default);
    Task<bool> DisableToDhcpAsync(NetworkAdapterInfo adapter, CancellationToken ct = default);
    Task<DnsState> GetStateAsync(NetworkAdapterInfo adapter, CancellationToken ct = default);
}
