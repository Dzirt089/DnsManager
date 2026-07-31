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
        // Предпочтительный: DoH вкл, авто-шаблон (без -DohTemplate), fallback откл.
        // Запись есть -> Set; записи нет -> Add.
        Assert.Contains("Set-DnsClientDohServerAddress -ServerAddress '111.88.96.50' -AutoUpgrade $true -AllowFallbackToUdp $false", script);
        Assert.Contains("Add-DnsClientDohServerAddress -ServerAddress '111.88.96.50' -AutoUpgrade $true -AllowFallbackToUdp $false", script);
        // Дополнительный: без DoH — удаляем устаревшую запись.
        Assert.Contains("Remove-DnsClientDohServerAddress -ServerAddress '111.88.96.51'", script);
    }

    [Fact]
    public void EnableManualScript_CustomTemplate_AddsDohTemplate()
    {
        var preset = new DnsPreset
        {
            Name = "Custom",
            Servers = [new DnsServerSetting { Address = "1.1.1.1", DohEnabled = true, DohTemplate = "https://cloudflare-dns.com/dns-query", AllowFallbackToUdp = true }]
        };

        var script = PowerShellCommandBuilder.EnableManualScript(1, preset);

        Assert.Contains("-DohTemplate 'https://cloudflare-dns.com/dns-query'", script);
        Assert.Contains("-AllowFallbackToUdp $true", script);
    }

    [Fact]
    public void DisableToDhcpScript_ResetsServersAndRemovesDohOfInterface()
    {
        var script = PowerShellCommandBuilder.DisableToDhcpScript(7);

        Assert.Contains("$idx=7", script);
        // Удаляет DoH только для адресов этого интерфейса, затем сброс в DHCP.
        Assert.Contains("Get-DnsClientServerAddress -AddressFamily IPv4 -InterfaceIndex $idx -ErrorAction SilentlyContinue).ServerAddresses", script);
        Assert.Contains("Remove-DnsClientDohServerAddress -ServerAddress $addr", script);
        Assert.Contains("Set-DnsClientServerAddress -InterfaceIndex $idx -ResetServerAddresses", script);
    }

    [Fact]
    public void GetStateScripts_UseInterfaceIndex()
    {
        Assert.Contains("-InterfaceIndex 42", PowerShellCommandBuilder.GetDnsServersScript(42));
        // DoH читается глобально по ServerAddress (без параметра интерфейса на этой ОС).
        Assert.Contains("Get-DnsClientDohServerAddress", PowerShellCommandBuilder.GetDohServersScript(42));
    }
}
