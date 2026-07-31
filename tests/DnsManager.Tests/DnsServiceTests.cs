using DnsManager.Core.Services;

namespace DnsManager.Tests;

public class DnsServiceTests
{
    [Fact]
    public void ParseDnsServers_ReturnsAddresses()
    {
        const string json = """
            {
              "InterfaceAlias": "Wi-Fi",
              "InterfaceIndex": 7,
              "ServerAddresses": [ "111.88.96.50", "111.88.96.51" ]
            }
            """;

        var result = DnsService.ParseDnsServers(json);

        Assert.Equal(2, result.Count);
        Assert.Contains("111.88.96.50", result);
        Assert.Contains("111.88.96.51", result);
    }

    [Fact]
    public void ParseDnsServers_EmptyJson_ReturnsEmpty()
    {
        Assert.Empty(DnsService.ParseDnsServers(""));
        Assert.Empty(DnsService.ParseDnsServers("{"));
        Assert.Empty(DnsService.ParseDnsServers("null"));
    }

    [Fact]
    public void ParseDohServers_FromInterfaceSettings()
    {
        const string json = """
            [
              { "ServerAddress": "111.88.96.50", "DohTemplate": "https://111.88.96.50/dns-query", "DohFlags": 1 },
              { "ServerAddress": "8.8.8.8", "DohTemplate": "https://dns.google/dns-query", "DohFlags": 2 }
            ]
            """;

        var result = DnsService.ParseDohServers(json);

        Assert.Equal(2, result.Count);
        var auto = result.First(s => s.Address == "111.88.96.50");
        Assert.True(auto.DohEnabled);
        Assert.True(auto.AutoUpgrade); // DohFlags=1 → авто-шаблон
        Assert.Equal("https://111.88.96.50/dns-query", auto.DohTemplate);
        var manual = result.First(s => s.Address == "8.8.8.8");
        Assert.False(manual.AutoUpgrade); // DohFlags=2 → вручную
    }

    [Fact]
    public void ParseDohServers_EmptyJson_ReturnsEmpty()
    {
        Assert.Empty(DnsService.ParseDohServers(""));
        Assert.Empty(DnsService.ParseDohServers("null"));
    }

    [Fact]
    public void ParseStaticDns_DetectsStaticVsDhcp()
    {
        Assert.True(DnsService.ParseStaticDns("""{"StaticDns":true}"""));
        Assert.False(DnsService.ParseStaticDns("""{"StaticDns":false}"""));
        Assert.False(DnsService.ParseStaticDns(""));
        Assert.False(DnsService.ParseStaticDns("not json"));
    }
}
