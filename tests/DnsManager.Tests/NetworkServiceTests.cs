using DnsManager.Core.Services;

namespace DnsManager.Tests;

public class NetworkServiceTests
{
    [Fact]
    public void ParseAdapters_SingleObject_ReturnsOne()
    {
        const string json = """
            {
              "Name": "Wi-Fi",
              "InterfaceDescription": "Intel(R) Wi-Fi 6 AX201",
              "InterfaceIndex": 7,
              "Status": "Up",
              "LinkSpeed": "450 Mbps",
              "MediaType": "802.11"
            }
            """;

        var result = NetworkService.ParseAdapters(json);

        var adapter = Assert.Single(result);
        Assert.Equal("Wi-Fi", adapter.Name);
        Assert.Equal(7, adapter.InterfaceIndex);
        Assert.Equal("802.11", adapter.MediaType);
        Assert.Equal("Wi-Fi", adapter.NetworkType);
    }

    [Fact]
    public void ParseAdapters_Array_Multiple()
    {
        const string json = """
            [
              { "Name": "Wi-Fi", "InterfaceIndex": 7, "Status": "Up", "MediaType": "802.11" },
              { "Name": "Ethernet", "InterfaceIndex": 8, "Status": "Disconnected", "MediaType": "Ethernet" }
            ]
            """;

        var result = NetworkService.ParseAdapters(json);

        Assert.Equal(2, result.Count);
        Assert.Equal("Wi-Fi", result[0].NetworkType);
        Assert.Equal("Ethernet", result[1].NetworkType);
    }

    [Fact]
    public void ParseAdapters_Empty_ReturnsEmpty()
    {
        Assert.Empty(NetworkService.ParseAdapters(""));
        Assert.Empty(NetworkService.ParseAdapters("null"));
    }

    [Fact]
    public void ParseProfiles_MapsFields()
    {
        const string json = """
            {
              "Name": "HomeWiFi",
              "InterfaceAlias": "Wi-Fi",
              "InterfaceIndex": 7,
              "NetworkCategory": "Private",
              "IPv4Connectivity": "Internet"
            }
            """;

        var result = NetworkService.ParseProfiles(json);

        var profile = Assert.Single(result);
        Assert.Equal("HomeWiFi", profile.ConnectionName);
        Assert.Equal("Private", profile.NetworkCategory);
        Assert.Equal("Internet", profile.IPv4Connectivity);
        Assert.True(profile.HasProfile);
    }
}
