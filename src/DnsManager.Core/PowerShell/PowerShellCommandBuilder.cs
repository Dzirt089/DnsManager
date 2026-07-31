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
    /// DoH-серверы, привязанные к интерфейсу: под-ключи DohInterfaceSettings\Doh
    /// (DohFlags: 1 = авто-шаблон, 2 = вручную) + шаблон из DohWellKnownServers.
    /// Именно DohInterfaceSettings читает Settings UI (метод HotCakeX/WinSecureDNSMgr).
    /// </summary>
    public static string GetDohServersScript(int interfaceIndex) =>
        Preamble +
        $"$idx={interfaceIndex};" +
        "$guid=(Get-NetAdapter -InterfaceIndex $idx -ErrorAction SilentlyContinue).InterfaceGuid;" +
        "$dohKey=\"HKLM:\\SYSTEM\\CurrentControlSet\\Services\\Dnscache\\InterfaceSpecificParameters\\$guid\\DohInterfaceSettings\\Doh\";" +
        "$wk=\"HKLM:\\SYSTEM\\CurrentControlSet\\Services\\Dnscache\\Parameters\\DohWellKnownServers\";" +
        "Get-ChildItem $dohKey -ErrorAction SilentlyContinue | ForEach-Object { " +
        "$ip=$_.PSChildName; " +
        "$flags=(Get-ItemProperty $_.PSPath -Name DohFlags -ErrorAction SilentlyContinue).DohFlags; " +
        "$template=(Get-ItemProperty \"$wk\\$ip\" -Name Template -ErrorAction SilentlyContinue).Template; " +
        "[PSCustomObject]@{ ServerAddress=$ip; DohTemplate=$template; DohFlags=$flags } } | ConvertTo-Json -Compress";

    /// <summary>
    /// Включить ручной DNS-профиль (пресет) с DoH:
    /// 1) Set-DnsClientServerAddress — серверы на интерфейсе;
    /// 2) Add/Set-DnsClientDohServerAddress — официальный метод (шаблон, AutoUpgrade, Fallback);
    /// 3) реестр DohWellKnownServers\&lt;ip&gt;\Template — виден UI/netsh;
    /// 4) реестр DohInterfaceSettings\Doh\&lt;ip&gt;\DohFlags=1 — привязка к интерфейсу (UI).
    /// </summary>
    public static string EnableManualScript(int interfaceIndex, DnsPreset preset)
    {
        var sb = new StringBuilder(Preamble);
        sb.Append($"$idx={interfaceIndex};");
        sb.Append("$guid=(Get-NetAdapter -InterfaceIndex $idx -ErrorAction SilentlyContinue).InterfaceGuid;");
        sb.Append("$dohKey=\"HKLM:\\SYSTEM\\CurrentControlSet\\Services\\Dnscache\\InterfaceSpecificParameters\\$guid\\DohInterfaceSettings\\Doh\";");
        sb.Append("$wk=\"HKLM:\\SYSTEM\\CurrentControlSet\\Services\\Dnscache\\Parameters\\DohWellKnownServers\";");

        var addresses = string.Join(",", preset.Servers.Select(s => $"'{s.Address}'"));
        sb.Append($"$addrs=@({addresses});");
        sb.Append("Set-DnsClientServerAddress -InterfaceIndex $idx -ServerAddresses $addrs;");

        foreach (var server in preset.Servers)
        {
            if (server.DohEnabled)
            {
                // ВАЖНО: на этой сборке Windows DohFlags=2 («вручную») показывает «Выключено».
                // Поэтому всегда используем DohFlags=1 (авто-шаблон) — как HotCakeX/WinSecureDNSMgr:
                // шаблон добавляется в предустановленный список, UI показывает «включено (автоматический шаблон)»,
                // а указанный пользователем шаблон используется как DoH-эндпоинт.
                var template = string.IsNullOrEmpty(server.DohTemplate)
                    ? $"https://{server.Address}/dns-query"
                    : server.DohTemplate;
                var fallback = server.AllowFallbackToUdp ? "$true" : "$false";

                // Шаблон в DohWellKnownServers — виден Settings UI (netsh dns show encryption).
                sb.Append($"New-Item -Path \"$wk\\{server.Address}\" -Force | Out-Null;");
                sb.Append($"New-ItemProperty -Path \"$wk\\{server.Address}\" -Name 'Template' -Value '{template}' -PropertyType String -Force | Out-Null;");

                // Официальный метод: запись как предустановленного DoH-провайдера (есть — Set, нет — Add).
                sb.Append($"try {{ Add-DnsClientDohServerAddress -ServerAddress '{server.Address}' -DohTemplate '{template}' -AllowFallbackToUdp {fallback} -AutoUpgrade $true -ErrorAction Stop }} catch {{ Set-DnsClientDohServerAddress -ServerAddress '{server.Address}' -DohTemplate '{template}' -AllowFallbackToUdp {fallback} -AutoUpgrade $true }};");

                // Привязка DoH к интерфейсу: DohFlags=1 (авто-шаблон) — именно это читает Settings UI.
                sb.Append($"New-Item -Path \"$dohKey\\{server.Address}\" -Force | Out-Null;");
                sb.Append($"New-ItemProperty -Path \"$dohKey\\{server.Address}\" -Name 'DohFlags' -Value 1 -PropertyType Qword -Force | Out-Null;");
            }
            else
            {
                // Сервер без DoH: убираем привязку к интерфейсу и записи DoH.
                sb.Append($"Remove-Item \"$dohKey\\{server.Address}\" -Recurse -Force -ErrorAction SilentlyContinue;");
                sb.Append($"Remove-Item \"$wk\\{server.Address}\" -Recurse -Force -ErrorAction SilentlyContinue;");
                sb.Append($"$doh=Get-DnsClientDohServerAddress -ServerAddress '{server.Address}' -ErrorAction SilentlyContinue;");
                sb.Append($"if($doh){{Remove-DnsClientDohServerAddress -ServerAddress '{server.Address}' -ErrorAction SilentlyContinue}};");
            }
        }

        sb.Append("'OK'");
        return sb.ToString();
    }

    /// <summary>Вернуть DNS интерфейса в режим «Автоматически (DHCP)» и убрать DoH-записи только для адресов этого интерфейса.</summary>
    public static string DisableToDhcpScript(int interfaceIndex) =>
        Preamble +
        $"$idx={interfaceIndex};" +
        "$guid=(Get-NetAdapter -InterfaceIndex $idx -ErrorAction SilentlyContinue).InterfaceGuid;" +
        "$dohKey=\"HKLM:\\SYSTEM\\CurrentControlSet\\Services\\Dnscache\\InterfaceSpecificParameters\\$guid\\DohInterfaceSettings\\Doh\";" +
        "$wk=\"HKLM:\\SYSTEM\\CurrentControlSet\\Services\\Dnscache\\Parameters\\DohWellKnownServers\";" +
        "foreach($addr in (Get-DnsClientServerAddress -AddressFamily IPv4 -InterfaceIndex $idx -ErrorAction SilentlyContinue).ServerAddresses){ " +
        "Remove-Item \"$dohKey\\$addr\" -Recurse -Force -ErrorAction SilentlyContinue; " +
        "Remove-Item \"$wk\\$addr\" -Recurse -Force -ErrorAction SilentlyContinue; " +
        "$doh=Get-DnsClientDohServerAddress -ServerAddress $addr -ErrorAction SilentlyContinue; " +
        "if($doh){Remove-DnsClientDohServerAddress -ServerAddress $addr -ErrorAction SilentlyContinue} }; " +
        "Set-DnsClientServerAddress -InterfaceIndex $idx -ResetServerAddresses; " +
        "'OK'";

    private static string Bool(bool value) => value ? "$true" : "$false";
}
