using System.Text.Json;
using DnsManager.Core.Models;

namespace DnsManager.Core.Services;

/// <summary>Пресеты в %LOCALAPPDATA%\DnsManager\presets.json; при отсутствии создаёт пресет по умолчанию из ТЗ.</summary>
public sealed class PresetStore : IPresetStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly string _filePath;

    public PresetStore(string? directory = null)
    {
        var dir = directory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DnsManager");
        _filePath = Path.Combine(dir, "presets.json");
    }

    public string FilePath => _filePath;

    public IReadOnlyList<DnsPreset> Load()
    {
        if (!File.Exists(_filePath))
        {
            var defaults = new List<DnsPreset> { DnsPreset.Default() };
            Save(defaults);
            return defaults;
        }

        try
        {
            var json = File.ReadAllText(_filePath);
            var presets = JsonSerializer.Deserialize<List<DnsPreset>>(json);
            return presets is { Count: > 0 } ? presets : [DnsPreset.Default()];
        }
        catch (JsonException)
        {
            return [DnsPreset.Default()];
        }
    }

    public void Save(IEnumerable<DnsPreset> presets)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
        File.WriteAllText(_filePath, JsonSerializer.Serialize(presets.ToList(), JsonOptions));
    }
}
