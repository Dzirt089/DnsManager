using System.Collections.ObjectModel;

namespace DnsManager.Core.Models;

/// <summary>Пресет DNS-профиля: именованный набор серверов.</summary>
public sealed record DnsPreset
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public string Name { get; set; } = "";

    public ObservableCollection<DnsServerSetting> Servers { get; init; } = [];

    /// <summary>Пресет по умолчанию из ТЗ.</summary>
    public static DnsPreset Default() => new()
    {
        Name = "111.88.96.50/51 (по умолчанию)",
        Servers =
        [
            DnsServerSetting.PrimaryProfile("111.88.96.50"),
            DnsServerSetting.SecondaryProfile("111.88.96.51")
        ]
    };
}
