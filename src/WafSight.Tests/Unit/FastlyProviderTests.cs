using FluentAssertions;
using WafSight.Models;
using WafSight.Providers;
using Xunit;

namespace WafSight.Tests.Unit;

public class FastlyProviderTests
{
    private readonly FastlyProvider _provider = new();

    [Fact]
    public async Task DetectAsync_WithFastlyTie_DetectsFastly()
    {
        var response = new HttpResponseData
        {
            StatusCode = 200,
            Headers = new Dictionary<string, string>
            {
                { "x-tie", "FRA" }
            }
        };

        var evidence = await _provider.PassiveDetectAsync(response);
        evidence.Should().Contain(e => e.Signature == "fastly-tie-header");
    }

    [Fact]
    public async Task DetectAsync_WithFastlySurrogateKey_DetectsFastly()
    {
        var response = new HttpResponseData
        {
            StatusCode = 200,
            Headers = new Dictionary<string, string>
            {
                { "x-surrogate-key", "abc123 def456" }
            }
        };

        var evidence = await _provider.PassiveDetectAsync(response);
        evidence.Should().Contain(e => e.Signature == "fastly-surrogate-key-header");
    }

    [Fact]
    public async Task DetectAsync_WithFastlyServer_DetectsServer()
    {
        var response = new HttpResponseData
        {
            StatusCode = 200,
            Headers = new Dictionary<string, string>
            {
                { "server", "Fastly" }
            }
        };

        var evidence = await _provider.PassiveDetectAsync(response);
        evidence.Should().Contain(e => e.Signature == "fastly-server-header");
    }

    [Fact]
    public async Task DetectAsync_NoFastlyHeaders_ReturnsEmpty()
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
        _provider.Name.Should().Be("Fastly");
        _provider.ProviderType.Should().Be(ProviderType.CDN);
        _provider.Priority.Should().Be(85);
    }
}
