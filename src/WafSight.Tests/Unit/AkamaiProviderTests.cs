using FluentAssertions;
using WafSight.Models;
using WafSight.Providers;
using Xunit;

namespace WafSight.Tests.Unit;

public class AkamaiProviderTests
{
    private readonly AkamaiProvider _provider = new();

    [Fact]
    public async Task DetectAsync_WithAkamaiTransformed_DetectsAkamai()
    {
        var response = new HttpResponseData
        {
            StatusCode = 200,
            Headers = new Dictionary<string, string>
            {
                { "x-akamai-transformed", "9 -" }
            }
        };

        var evidence = await _provider.PassiveDetectAsync(response);
        evidence.Should().Contain(e => e.Signature == "x-akamai-transformed-header");
    }

    [Fact]
    public async Task DetectAsync_WithAkamaiServer_DetectsServer()
    {
        var response = new HttpResponseData
        {
            StatusCode = 200,
            Headers = new Dictionary<string, string>
            {
                { "server", "AkamaiG2" }
            }
        };

        var evidence = await _provider.PassiveDetectAsync(response);
        evidence.Should().Contain(e => e.Signature == "akamai-server-header");
    }

    [Fact]
    public async Task DetectAsync_NoAkamaiHeaders_ReturnsEmpty()
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
        _provider.Name.Should().Be("Akamai");
        _provider.ProviderType.Should().Be(ProviderType.Both);
        _provider.Enabled.Should().BeTrue();
        _provider.Priority.Should().Be(90);
    }
}
