using FluentAssertions;
using WafSight.Models;
using WafSight.Providers;
using Xunit;

namespace WafSight.Tests.Unit;

public class CloudFlareProviderTests
{
    private readonly CloudFlareProvider _provider = new();

    [Fact]
    public async Task DetectAsync_WithCfRayHeader_DetectsCloudFlare()
    {
        var response = new HttpResponseData
        {
            StatusCode = 200,
            Headers = new Dictionary<string, string>
            {
                { "cf-ray", "abc123-CDG" },
                { "server", "cloudflare" }
            },
            Body = "OK"
        };

        var evidence = await _provider.PassiveDetectAsync(response);
        evidence.Should().NotBeEmpty();
        evidence.Should().Contain(e => e.Signature == "cf-ray-header");
    }

    [Fact]
    public async Task DetectAsync_WithCfCacheStatus_DetectsCloudFlare()
    {
        var response = new HttpResponseData
        {
            StatusCode = 200,
            Headers = new Dictionary<string, string>
            {
                { "cf-cache-status", "HIT" }
            }
        };

        var evidence = await _provider.PassiveDetectAsync(response);
        evidence.Should().Contain(e => e.Signature == "cf-cache-status-header");
    }

    [Fact]
    public async Task DetectAsync_WithCloudFlareCookie_DetectsCookie()
    {
        var response = new HttpResponseData
        {
            StatusCode = 200,
            Headers = new Dictionary<string, string>
            {
                { "set-cookie", "__cfduid=abc123; __cf_bm=def456" }
            }
        };

        var evidence = await _provider.PassiveDetectAsync(response);
        evidence.Should().Contain(e => e.Signature == "cloudflare-cookie");
    }

    [Fact]
    public async Task DetectAsync_With403AndCfRay_DetectsStatusCode()
    {
        var response = new HttpResponseData
        {
            StatusCode = 403,
            Headers = new Dictionary<string, string>
            {
                { "cf-ray", "abc123-CDG" }
            },
            Body = "Access Denied - CloudFlare"
        };

        var evidence = await _provider.PassiveDetectAsync(response);
        evidence.Should().Contain(e => e.Signature == "cf-403-status");
    }

    [Fact]
    public async Task DetectAsync_NoHeaders_ReturnsEmptyEvidence()
    {
        var response = new HttpResponseData
        {
            StatusCode = 200,
            Headers = new Dictionary<string, string>(),
            Body = "OK"
        };

        var evidence = await _provider.PassiveDetectAsync(response);
        evidence.Should().BeEmpty();
    }

    [Fact]
    public void Metadata_ReturnsCorrectValues()
    {
        _provider.Name.Should().Be("CloudFlare");
        _provider.ProviderType.Should().Be(ProviderType.Both);
        _provider.Enabled.Should().BeTrue();
        _provider.Priority.Should().Be(100);
    }
}
