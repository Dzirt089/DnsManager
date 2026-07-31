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

    /// <summary>
    /// Настроены ли DNS статически (реестровый NameServer пуст при DHCP).
    /// Используется для корректного определения режима DHCP vs ручной.
    /// </summary>
    public static string GetStaticDnsScript(int interfaceIndex) =>
        Preamble +
        $"$idx={interfaceIndex};" +
        "$guid=(Get-NetAdapter -InterfaceIndex $idx -ErrorAction SilentlyContinue).InterfaceGuid;" +
        "$ns=(Get-ItemProperty \"HKLM:\\SYSTEM\\CurrentControlSet\\Services\\Tcpip\\Parameters\\Interfaces\\$guid\" -Name NameServer -ErrorAction SilentlyContinue).NameServer;" +
        "[PSCustomObject]@{ StaticDns = ![string]::IsNullOrEmpty($ns) } | ConvertTo-Json -Compress";

    /// <summary>
    /// DoH-серверы из DohWellKnownServers (реестр) — именно этот список читает
    /// Settings UI (netsh dns show encryption). CIM-команды *-DnsClientDohServerAddress
    /// пишут в другой стор и не отображаются в UI, поэтому не используются.
    /// </summary>
    public static string GetDohServersScript() =>
        Preamble + "Get-ChildItem 'HKLM:\\SYSTEM\\CurrentControlSet\\Services\\Dnscache\\Parameters\\DohWellKnownServers' -ErrorAction SilentlyContinue | ForEach-Object { [PSCustomObject]@{ ServerAddress=$_.PSChildName; DohTemplate=$_.GetValue('Template') } } | ConvertTo-Json -Compress";

    /// <summary>Включить ручной DNS-профиль (пресет) с DoH-настройками (netsh dns add/set/delete encryption).</summary>
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
                // «Автоматический шаблон» = https://<ip>/dns-query; явный шаблон пользователя не трогаем.
                var template = string.IsNullOrEmpty(server.DohTemplate)
                    ? $"https://{server.Address}/dns-query"
                    : server.DohTemplate;
                var fallback = server.AllowFallbackToUdp ? "yes" : "no";
                // netsh пишет в DohWellKnownServers — то, что видит Settings UI.
                // Если запись уже есть (add не прошёл) — обновляем через set.
                sb.Append($"netsh dns add encryption server='{server.Address}' dohtemplate='{template}' autoupgrade=yes udpfallback={fallback} *> $null;");
                sb.Append($"if ($LASTEXITCODE -ne 0) {{ netsh dns set encryption server='{server.Address}' dohtemplate='{template}' autoupgrade=yes udpfallback={fallback} *> $null; if ($LASTEXITCODE -ne 0) {{ throw \"DoH: не удалось настроить '{server.Address}' (netsh $LASTEXITCODE)\" }} }};");
            }
            else
            {
                // Сервер без DoH: удаляем запись из списка secure-резолверов (если была).
                sb.Append($"netsh dns delete encryption server='{server.Address}' *> $null;");
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
        "netsh dns delete encryption server=$addr *> $null }; " +
        "Set-DnsClientServerAddress -InterfaceIndex $idx -ResetServerAddresses; " +
        "'OK'";

    private static string Bool(bool value) => value ? "$true" : "$false";
}
