using System.Text;
using DnsManager.Core.Models;

namespace DnsManager.Core.PowerShell;

/// <summary>Построение PowerShell-скриптов для чтения/изменения настроек DNS и сети.</summary>
public static class PowerShellCommandBuilder
{
    private const string Preamble =
        "$ErrorActionPreference='Stop'; [Console]::OutputEncoding=[Text.Encoding]::UTF8;";

    /// <summary>Все адаптеры (Get-NetAdapter).</summary>
    public static string GetAdaptersScript() =>
        Preamble + "Get-NetAdapter | Select-Object Name,InterfaceDescription,InterfaceIndex,Status,LinkSpeed,MediaType | ConvertTo-Json -Compress";

    /// <summary>Профили подключений (Get-NetConnectionProfile) — для определения активного адаптера и типа сети.</summary>
    public static string GetProfilesScript() =>
        Preamble + "Get-NetConnectionProfile | ForEach-Object { [PSCustomObject]@{ Name=$_.Name; InterfaceAlias=$_.InterfaceAlias; InterfaceIndex=$_.InterfaceIndex; NetworkCategory=$_.NetworkCategory.ToString(); IPv4Connectivity=$_.IPv4Connectivity.ToString() } } | ConvertTo-Json -Compress";

    /// <summary>Текущие IPv4 DNS-серверы интерфейса.</summary>
    public static string GetDnsServersScript(int interfaceIndex) =>
        Preamble + $"Get-DnsClientServerAddress -AddressFamily IPv4 -InterfaceIndex {interfaceIndex} | Select-Object InterfaceAlias,InterfaceIndex,ServerAddresses | ConvertTo-Json -Compress";

    /// <summary>Текущие DoH-настройки интерфейса.</summary>
    public static string GetDohServersScript(int interfaceIndex) =>
        Preamble + $"Get-DnsClientDohServerAddress -InterfaceIndex {interfaceIndex} -ErrorAction SilentlyContinue | Select-Object InterfaceAlias,InterfaceIndex,ServerAddress,DohTemplate,AutoUpgrade,AllowFallbackToUdp | ConvertTo-Json -Compress";

    /// <summary>Включить ручной DNS-профиль (пресет) с DoH-настройками.</summary>
    public static string EnableManualScript(int interfaceIndex, DnsPreset preset)
    {
        var sb = new StringBuilder(Preamble);
        sb.Append($"$idx={interfaceIndex};");

        var addresses = string.Join(",", preset.Servers.Select(s => $"'{s.Address}'"));
        sb.Append($"$addrs=@({addresses});");
        sb.Append("Set-DnsClientServerAddress -InterfaceIndex $idx -ServerAddresses $addrs;");

        foreach (var server in preset.Servers)
        {
            if (server.DohEnabled)
            {
                var template = string.IsNullOrEmpty(server.DohTemplate) ? "" : $" -DohTemplate '{server.DohTemplate}'";
                // DoH-команды работают глобально по ServerAddress: если запись есть — Set, иначе — Add.
                sb.Append($"$doh=Get-DnsClientDohServerAddress -ServerAddress '{server.Address}' -ErrorAction SilentlyContinue;");
                sb.Append($"if($doh){{Set-DnsClientDohServerAddress -ServerAddress '{server.Address}' -AutoUpgrade $true -AllowFallbackToUdp {Bool(server.AllowFallbackToUdp)}{template}}}");
                sb.Append($"else{{Add-DnsClientDohServerAddress -ServerAddress '{server.Address}' -AutoUpgrade $true -AllowFallbackToUdp {Bool(server.AllowFallbackToUdp)}{template}}};");
            }
            else
            {
                // Сервер без DoH: удаляем устаревшую DoH-запись, если была.
                sb.Append($"$doh=Get-DnsClientDohServerAddress -ServerAddress '{server.Address}' -ErrorAction SilentlyContinue;");
                sb.Append($"if($doh){{Remove-DnsClientDohServerAddress -ServerAddress '{server.Address}'}};");
            }
        }

        sb.Append("'OK'");
        return sb.ToString();
    }

    /// <summary>Вернуть DNS интерфейса в режим «Автоматически (DHCP)» и убрать DoH-записи только для адресов этого интерфейса.</summary>
    public static string DisableToDhcpScript(int interfaceIndex) =>
        Preamble +
        $"$idx={interfaceIndex};" +
        "foreach($addr in (Get-DnsClientServerAddress -AddressFamily IPv4 -InterfaceIndex $idx -ErrorAction SilentlyContinue).ServerAddresses){ " +
        "$doh=Get-DnsClientDohServerAddress -ServerAddress $addr -ErrorAction SilentlyContinue; " +
        "if($doh){Remove-DnsClientDohServerAddress -ServerAddress $addr} }; " +
        "Set-DnsClientServerAddress -InterfaceIndex $idx -ResetServerAddresses; " +
        "'OK'";

    private static string Bool(bool value) => value ? "$true" : "$false";
}
