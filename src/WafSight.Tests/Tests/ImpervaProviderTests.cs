using FluentAssertions;
using WafSight.Models;
using WafSight.Providers;
using Xunit;

namespace WafSight.Tests.Tests;

public class ImpervaProviderTests
{
    private readonly ImpervaProvider _provider = new();

    [Fact]
    public async Task DetectAsync_WithImpervaCdn_DetectsImperva()
    {
        var response = new HttpResponseData
        {
            StatusCode = 200,
            Headers = new Dictionary<string, string>
            {
                { "x-cdn", "INCAP" }
            }
        };

        var evidence = await _provider.PassiveDetectAsync(response);
        evidence.Should().Contain(e => e.Signature == "x-cdn-header");
    }

    [Fact]
    public async Task DetectAsync_WithImpervaCookies_DetectsCookies()
    {
        var response = new HttpResponseData
        {
            StatusCode = 200,
            Headers = new Dictionary<string, string>
            {
                { "set-cookie", "__akavpau_abc=123; visid_incap=def456" }
            }
        };

        var evidence = await _provider.PassiveDetectAsync(response);
        evidence.Should().Contain(e => e.Signature == "imperva-captcha-cookie");
    }

    [Fact]
    public async Task DetectAsync_WithImpervaServer_DetectsServer()
    {
        var response = new HttpResponseData
        {
            StatusCode = 200,
            Headers = new Dictionary<string, string>
            {
                { "server", "incap-s-info" }
            }
        };

        var evidence = await _provider.PassiveDetectAsync(response);
        evidence.Should().Contain(e => e.Signature == "imperva-server-header");
    }

    [Fact]
    public async Task DetectAsync_NoImpervaHeaders_ReturnsEmpty()
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
        _provider.Name.Should().Be("Imperva");
        _provider.ProviderType.Should().Be(ProviderType.WAF);
        _provider.Priority.Should().Be(75);
    }
}
