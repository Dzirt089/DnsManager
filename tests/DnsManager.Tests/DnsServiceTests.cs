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
    public void ParseDohServers_MapsFlags()
    {
        const string json = """
            {
              "ServerAddress": "111.88.96.50",
              "DohTemplate": "",
              "AutoUpgrade": true,
              "AllowFallbackToUdp": false
            }
            """;

        var result = DnsService.ParseDohServers(json);

        var server = Assert.Single(result);
        Assert.Equal("111.88.96.50", server.Address);
        Assert.True(server.DohEnabled);
        Assert.True(server.AutoUpgrade);
        Assert.False(server.AllowFallbackToUdp);
    }
}
