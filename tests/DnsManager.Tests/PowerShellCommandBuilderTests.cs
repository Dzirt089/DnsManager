using DnsManager.Core.Models;
using DnsManager.Core.PowerShell;

namespace DnsManager.Tests;

public class PowerShellCommandBuilderTests
{
    [Fact]
    public void EnableManualScript_DefaultPreset_SetsBothServersAndDoh()
    {
        var preset = DnsPreset.Default();
        var script = PowerShellCommandBuilder.EnableManualScript(7, preset);

        Assert.Contains("$idx=7", script);
        Assert.Contains("Set-DnsClientServerAddress -InterfaceIndex $idx -ServerAddresses $addrs", script);
        Assert.Contains("'111.88.96.50'", script);
        Assert.Contains("'111.88.96.51'", script);
        // Предпочтительный: DoH вкл, «автоматический шаблон» (https://<ip>/dns-query), fallback откл.
        Assert.Contains("netsh dns add encryption server='111.88.96.50' dohtemplate='https://111.88.96.50/dns-query' autoupgrade=yes udpfallback=no", script);
        Assert.Contains("netsh dns set encryption server='111.88.96.50' dohtemplate='https://111.88.96.50/dns-query' autoupgrade=yes udpfallback=no", script);
        // Дополнительный: без DoH — удаляем запись из списка secure-резолверов.
        Assert.Contains("netsh dns delete encryption server='111.88.96.51'", script);
    }

    [Fact]
    public void EnableManualScript_CustomTemplate_IsUsedAsIs()
    {
        var preset = new DnsPreset
        {
            Name = "Custom",
            Servers = [new DnsServerSetting { Address = "1.1.1.1", DohEnabled = true, DohTemplate = "https://cloudflare-dns.com/dns-query", AllowFallbackToUdp = true }]
        };

        var script = PowerShellCommandBuilder.EnableManualScript(1, preset);

        Assert.Contains("dohtemplate='https://cloudflare-dns.com/dns-query'", script);
        Assert.Contains("udpfallback=yes", script);
        // Свой шаблон не подменяется «автоматическим».
        Assert.DoesNotContain("https://1.1.1.1/dns-query", script);
    }

    [Fact]
    public void EnableManualScript_NoTemplate_ExpandsToAutoDnsQuery()
    {
        var preset = new DnsPreset
        {
            Name = "Auto",
            Servers = [new DnsServerSetting { Address = "8.8.8.8", DohEnabled = true }]
        };

        var script = PowerShellCommandBuilder.EnableManualScript(1, preset);

        Assert.Contains("dohtemplate='https://8.8.8.8/dns-query'", script);
    }

    [Fact]
    public void DisableToDhcpScript_ResetsServersAndRemovesDohOfInterface()
    {
        var script = PowerShellCommandBuilder.DisableToDhcpScript(7);

        Assert.Contains("$idx=7", script);
        // Удаляет DoH (netsh delete) для адресов этого интерфейса, затем сброс в DHCP.
        Assert.Contains("Get-DnsClientServerAddress -AddressFamily IPv4 -InterfaceIndex $idx -ErrorAction SilentlyContinue).ServerAddresses", script);
        Assert.Contains("netsh dns delete encryption server=$addr", script);
        Assert.Contains("Set-DnsClientServerAddress -InterfaceIndex $idx -ResetServerAddresses", script);
    }

    [Fact]
    public void GetStateScripts_UseCorrectSources()
    {
        // DNS-серверы интерфейса — по индексу.
        Assert.Contains("-InterfaceIndex 42", PowerShellCommandBuilder.GetDnsServersScript(42));
        // Статическая настройка — по реестру NameServer.
        Assert.Contains("$idx=42", PowerShellCommandBuilder.GetStaticDnsScript(42));
        // DoH читается из DohWellKnownServers (реестр) — источник Settings UI.
        Assert.Contains("DohWellKnownServers", PowerShellCommandBuilder.GetDohServersScript());
    }
}
