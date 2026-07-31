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
        // Предпочтительный: DoH вкл, «автоматический шаблон», fallback откл.
        Assert.Contains("Add-DnsClientDohServerAddress -ServerAddress '111.88.96.50' -DohTemplate 'https://111.88.96.50/dns-query' -AllowFallbackToUdp $false -AutoUpgrade $true", script);
        // Шаблон в DohWellKnownServers (виден netsh/UI).
        Assert.Contains("New-ItemProperty -Path \"$wk\\111.88.96.50\" -Name 'Template' -Value 'https://111.88.96.50/dns-query' -PropertyType String", script);
        // Привязка к интерфейсу: DohFlags=1 (авто-шаблон) — читает Settings UI.
        Assert.Contains("New-ItemProperty -Path \"$dohKey\\111.88.96.50\" -Name 'DohFlags' -Value 1 -PropertyType Qword", script);
        // Дополнительный: без DoH — убираем привязку и записи.
        Assert.Contains("Remove-Item \"$dohKey\\111.88.96.51\"", script);
        Assert.Contains("Remove-DnsClientDohServerAddress -ServerAddress '111.88.96.51'", script);
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

        Assert.Contains("-DohTemplate 'https://cloudflare-dns.com/dns-query'", script);
        Assert.Contains("-AllowFallbackToUdp $true", script);
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

        Assert.Contains("-DohTemplate 'https://8.8.8.8/dns-query'", script);
    }

    [Fact]
    public void DisableToDhcpScript_RemovesDohAndResetsServers()
    {
        var script = PowerShellCommandBuilder.DisableToDhcpScript(7);

        Assert.Contains("$idx=7", script);
        // Удаляет DoH (реестр + CIM) для адресов этого интерфейса, затем сброс в DHCP.
        Assert.Contains("Get-DnsClientServerAddress -AddressFamily IPv4 -InterfaceIndex $idx -ErrorAction SilentlyContinue).ServerAddresses", script);
        Assert.Contains("Remove-Item \"$dohKey\\$addr\"", script);
        Assert.Contains("Remove-DnsClientDohServerAddress -ServerAddress $addr", script);
        Assert.Contains("Set-DnsClientServerAddress -InterfaceIndex $idx -ResetServerAddresses", script);
    }

    [Fact]
    public void GetStateScripts_UseCorrectSources()
    {
        Assert.Contains("-InterfaceIndex 42", PowerShellCommandBuilder.GetDnsServersScript(42));
        Assert.Contains("$idx=42", PowerShellCommandBuilder.GetStaticDnsScript(42));
        // DoH читается из per-interface DohInterfaceSettings (источник Settings UI).
        var doh = PowerShellCommandBuilder.GetDohServersScript(42);
        Assert.Contains("DohInterfaceSettings", doh);
        Assert.Contains("$idx=42", doh);
    }
}
