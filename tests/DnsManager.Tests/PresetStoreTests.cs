using DnsManager.Core.Models;
using DnsManager.Core.Services;

namespace DnsManager.Tests;

public class PresetStoreTests
{
	[Fact]
	public void Load_WhenFileMissing_CreatesDefaultPreset()
	{
		var dir = Path.Combine(Path.GetTempPath(), "dnsmanager-tests", Guid.NewGuid().ToString("N"));
		var store = new PresetStore(dir);

		var presets = store.Load();

		Assert.Single(presets);
		Assert.Equal(DnsPreset.Default().Name, presets[0].Name);
		Assert.Equal(2, presets[0].Servers.Count);
		Assert.True(File.Exists(store.FilePath));

		Directory.Delete(dir, recursive: true);
	}

	[Fact]
	public void SaveAndLoad_RoundTrip_PreservesData()
	{
		var dir = Path.Combine(Path.GetTempPath(), "dnsmanager-tests", Guid.NewGuid().ToString("N"));
		var store = new PresetStore(dir);
		store.Load(); // создаёт файл

		var custom = new DnsPreset
		{
			Name = "Cloudflare",
			Servers =
			[
				new DnsServerSetting { Address = "1.1.1.1", DohEnabled = true, AllowFallbackToUdp = false },
				new DnsServerSetting { Address = "1.0.0.1", DohEnabled = false }
			]
		};
		store.Save([custom]);

		var loaded = new PresetStore(dir).Load();

		var preset = Assert.Single(loaded);
		Assert.Equal("Cloudflare", preset.Name);
		Assert.Equal(2, preset.Servers.Count);
		Assert.True(preset.Servers[0].DohEnabled);
		Assert.False(preset.Servers[0].AllowFallbackToUdp);
		Assert.False(preset.Servers[1].DohEnabled);

		Directory.Delete(dir, recursive: true);
	}

	[Fact]
	public void Load_CorruptedJson_ReturnsDefault()
	{
		var dir = Path.Combine(Path.GetTempPath(), "dnsmanager-tests", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(dir);
		File.WriteAllText(Path.Combine(dir, "presets.json"), "not a json {{{");

		var store = new PresetStore(dir);
		var presets = store.Load();

		Assert.Single(presets);

		Directory.Delete(dir, recursive: true);
	}
}
