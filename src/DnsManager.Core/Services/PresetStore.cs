using DnsManager.Core.Models;

using System.Text.Json;

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
			return presets is { Count: > 0 } ? EnsureSingleDefault(presets) : [DnsPreset.Default()];
		}
		catch (JsonException)
		{
			return [DnsPreset.Default()];
		}
	}

	/// <summary>Гарантирует ровно один default-профиль: если нет — назначает первый, если несколько — оставляет первый.</summary>
	private List<DnsPreset> EnsureSingleDefault(List<DnsPreset> presets)
	{
		var firstDefault = presets.FirstOrDefault(p => p.IsDefault);
		if (firstDefault is null)
		{
			presets[0].IsDefault = true;
			Save(presets);
			return presets;
		}

		var changed = false;
		foreach (var preset in presets.Where(p => p != firstDefault && p.IsDefault))
		{
			preset.IsDefault = false;
			changed = true;
		}

		if (changed)
			Save(presets);

		return presets;
	}

	public void Save(IEnumerable<DnsPreset> presets)
	{
		Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
		File.WriteAllText(_filePath, JsonSerializer.Serialize(presets.ToList(), JsonOptions));
	}
}
