using DnsManager.Core.Models;

using System.Text.Json;
using System.Text.Json.Serialization;

namespace DnsManager.Core.Services;

/// <summary>
/// Настройки UI окна в %LOCALAPPDATA%\DnsManager\uisettings.json.
/// При отсутствии файла или повреждённом JSON возвращает значения по умолчанию.
/// </summary>
public sealed class UiSettingsStore
{
	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		WriteIndented = true,
		Converters = { new JsonStringEnumConverter() }
	};

	private readonly string _filePath;

	public UiSettingsStore(string? directory = null)
	{
		var dir = directory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DnsManager");
		_filePath = Path.Combine(dir, "uisettings.json");
	}

	public string FilePath => _filePath;

	public UiSettings Load()
	{
		if (!File.Exists(_filePath))
			return new UiSettings();

		try
		{
			var json = File.ReadAllText(_filePath);
			return JsonSerializer.Deserialize<UiSettings>(json, JsonOptions) ?? new UiSettings();
		}
		catch (JsonException)
		{
			return new UiSettings();
		}
	}

	public void Save(UiSettings settings)
	{
		Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
		File.WriteAllText(_filePath, JsonSerializer.Serialize(settings, JsonOptions));
	}
}
