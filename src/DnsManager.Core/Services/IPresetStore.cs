using DnsManager.Core.Models;

namespace DnsManager.Core.Services;

/// <summary>Хранение пресетов DNS-профилей (JSON).</summary>
public interface IPresetStore
{
    IReadOnlyList<DnsPreset> Load();
    void Save(IEnumerable<DnsPreset> presets);
    string FilePath { get; }
}
