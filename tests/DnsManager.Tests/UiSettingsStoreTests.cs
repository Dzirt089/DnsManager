using DnsManager.Core.Models;
using DnsManager.Core.Services;

namespace DnsManager.Tests;

public class UiSettingsStoreTests
{
    [Fact]
    public void Load_WhenFileMissing_ReturnsDefaults()
    {
        var dir = Path.Combine(Path.GetTempPath(), "dnsmanager-tests", Guid.NewGuid().ToString("N"));
        var store = new UiSettingsStore(dir);

        var settings = store.Load();

        Assert.Equal(UiSettings.DefaultWindowWidth, settings.WindowWidth);
        Assert.Equal(UiSettings.DefaultWindowHeight, settings.WindowHeight);
        Assert.Null(settings.WindowLeft);
        Assert.Null(settings.WindowTop);
        Assert.False(settings.IsMaximized);
        Assert.Equal(UiTheme.Light, settings.Theme);
        Assert.Equal(UiSettings.DefaultLogPanelHeight, settings.LogPanelHeight);
        Assert.Equal(UiSettings.DefaultPresetListWidth, settings.PresetListWidth);
        Assert.Equal(UiSettings.DefaultDnsPanelHeight, settings.DnsPanelHeight);
        Assert.False(File.Exists(store.FilePath));

        if (Directory.Exists(dir))
            Directory.Delete(dir, recursive: true);
    }

    [Fact]
    public void SaveAndLoad_RoundTrip_PreservesData()
    {
        var dir = Path.Combine(Path.GetTempPath(), "dnsmanager-tests", Guid.NewGuid().ToString("N"));
        var store = new UiSettingsStore(dir);

        store.Save(new UiSettings
        {
            WindowWidth = 1280,
            WindowHeight = 800,
            WindowLeft = 120,
            WindowTop = 80,
            IsMaximized = true,
            Theme = UiTheme.Dark,
            LogPanelHeight = 320,
            PresetListWidth = 300,
            DnsPanelHeight = 210
        });

        var loaded = new UiSettingsStore(dir).Load();

        Assert.Equal(1280, loaded.WindowWidth);
        Assert.Equal(800, loaded.WindowHeight);
        Assert.Equal(120, loaded.WindowLeft);
        Assert.Equal(80, loaded.WindowTop);
        Assert.True(loaded.IsMaximized);
        Assert.Equal(UiTheme.Dark, loaded.Theme);
        Assert.Equal(320, loaded.LogPanelHeight);
        Assert.Equal(300, loaded.PresetListWidth);
        Assert.Equal(210, loaded.DnsPanelHeight);

        Directory.Delete(dir, recursive: true);
    }

    [Fact]
    public void Load_CorruptedJson_ReturnsDefaults()
    {
        var dir = Path.Combine(Path.GetTempPath(), "dnsmanager-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "uisettings.json"), "not a json {{{");

        var store = new UiSettingsStore(dir);
        var settings = store.Load();

        Assert.Equal(UiTheme.Light, settings.Theme);
        Assert.Equal(UiSettings.DefaultWindowWidth, settings.WindowWidth);

        Directory.Delete(dir, recursive: true);
    }
}
