namespace DnsManager.Core.Models;

/// <summary>Настройка одного DNS-сервера в пресете.</summary>
public sealed record DnsServerSetting
{
    public string Address { get; set; } = "";

    /// <summary>DoH включён для этого сервера.</summary>
    public bool DohEnabled { get; set; }

    /// <summary>Явный DoH-шаблон; null = «автоматический шаблон» (https://&lt;ip&gt;/dns-query).</summary>
    public string? DohTemplate { get; set; }

    /// <summary>Разрешить возврат к обычному (нешифрованному) тексту, если DoH недоступен.</summary>
    public bool AllowFallbackToUdp { get; set; } = true;

    /// <summary>Профиль из ТЗ: предпочтительный сервер с DoH (авто-шаблон, без fallback).</summary>
    public static DnsServerSetting PrimaryProfile(string address) =>
        new() { Address = address, DohEnabled = true, DohTemplate = null, AllowFallbackToUdp = false };

    /// <summary>Профиль из ТЗ: дополнительный сервер без DoH.</summary>
    public static DnsServerSetting SecondaryProfile(string address) =>
        new() { Address = address, DohEnabled = false };
}
