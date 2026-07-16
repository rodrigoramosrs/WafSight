using FluentAssertions;
using WafSight.Models;
using WafSight.Providers;
using Xunit;

namespace WafSight.Tests.Tests;

public class SucuriProviderTests
{
    private readonly SucuriProvider _provider = new();

    [Fact]
    public async Task DetectAsync_WithSucuriId_DetectsSucuri()
    {
        var response = new HttpResponseData
        {
            StatusCode = 200,
            Headers = new Dictionary<string, string>
            {
                { "x-sucuri-id", "10001" }
            }
        };

        var evidence = await _provider.PassiveDetectAsync(response);
        evidence.Should().Contain(e => e.Signature == "x-sucuri-id-header");
    }

    [Fact]
    public async Task DetectAsync_WithSucuriCache_DetectsCache()
    {
        var response = new HttpResponseData
        {
            StatusCode = 200,
            Headers = new Dictionary<string, string>
            {
                { "x-sucuri-cache", "HIT" }
            }
        };

        var evidence = await _provider.PassiveDetectAsync(response);
        evidence.Should().Contain(e => e.Signature == "x-sucuri-cache-header");
    }

    [Fact]
    public async Task DetectAsync_WithSucuriBlock_DetectsBlock()
    {
        var response = new HttpResponseData
        {
            StatusCode = 200,
            Headers = new Dictionary<string, string>
            {
                { "x-sucuri-block", "Yes" }
            }
        };

        var evidence = await _provider.PassiveDetectAsync(response);
        evidence.Should().Contain(e => e.Signature == "x-sucuri-block-header");
    }

    [Fact]
    public async Task DetectAsync_WithSucuriBody_DetectsBody()
    {
        var response = new HttpResponseData
        {
            StatusCode = 200,
            Body = "<html>Access denied - sucuri webserver firewall</html>"
        };

        var evidence = await _provider.PassiveDetectAsync(response);
        evidence.Should().Contain(e => e.Signature == "sucuri-body");
    }

    [Fact]
    public async Task DetectAsync_NoSucuriHeaders_ReturnsEmpty()
    {
        var response = new HttpResponseData
        {
            StatusCode = 200,
            Headers = new Dictionary<string, string>
            {
                { "server", "nginx" }
            }
        };

        var evidence = await _provider.PassiveDetectAsync(response);
        evidence.Should().BeEmpty();
    }

    [Fact]
    public void Metadata_ReturnsCorrectValues()
    {
        _provider.Name.Should().Be("Sucuri");
        _provider.ProviderType.Should().Be(ProviderType.WAF);
        _provider.Priority.Should().Be(70);
    }
}
