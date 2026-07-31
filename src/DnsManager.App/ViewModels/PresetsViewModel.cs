using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DnsManager.Core.Logging;
using DnsManager.Core.Models;
using DnsManager.Core.Services;

namespace DnsManager.App.ViewModels;

/// <summary>Управление пресетами DNS-профилей: список, CRUD, сохранение в JSON.</summary>
public sealed partial class PresetsViewModel : ObservableObject
{
    private readonly IPresetStore _store;
    private readonly ILogService _log;

    public ObservableCollection<DnsPreset> Presets { get; } = [];

    [ObservableProperty]
    private DnsPreset? _selectedPreset;

    public PresetsViewModel(IPresetStore store, ILogService log)
    {
        _store = store;
        _log = log;
        foreach (var preset in store.Load())
            Presets.Add(preset);
        SelectedPreset = Presets.FirstOrDefault();
    }

    [RelayCommand]
    private void AddPreset()
    {
        var preset = new DnsPreset
        {
            Name = "Новый пресет",
            Servers =
            [
                DnsServerSetting.PrimaryProfile("8.8.8.8"),
                DnsServerSetting.SecondaryProfile("8.8.4.4")
            ]
        };
        Presets.Add(preset);
        SelectedPreset = preset;
        Save();
        _log.Info(LogEvents.PresetCreate, $"Пресет «{preset.Name}» создан.", ("Preset", preset.Name));
    }

    [RelayCommand]
    private void DeletePreset(DnsPreset? preset)
    {
        if (preset is null || Presets.Count <= 1)
            return;
        Presets.Remove(preset);
        SelectedPreset = Presets.FirstOrDefault();
        Save();
        _log.Info(LogEvents.PresetDelete, $"Пресет «{preset.Name}» удалён.", ("Preset", preset.Name));
    }

    [RelayCommand]
    private void AddServer()
    {
        if (SelectedPreset is null)
            return;
        SelectedPreset.Servers.Add(new DnsServerSetting { Address = "1.1.1.1" });
        Save();
    }

    [RelayCommand]
    private void RemoveServer(DnsServerSetting? server)
    {
        if (SelectedPreset is null || server is null)
            return;
        SelectedPreset.Servers.Remove(server);
        Save();
    }

    [RelayCommand]
    private void SavePresets() => Save();

    public void Save() => _store.Save(Presets);
}
