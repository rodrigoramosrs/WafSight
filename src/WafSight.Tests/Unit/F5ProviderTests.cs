using FluentAssertions;
using WafSight.Models;
using WafSight.Providers;
using Xunit;

namespace WafSight.Tests.Unit;

public class F5ProviderTests
{
    private readonly F5Provider _provider = new();

    [Fact]
    public async Task DetectAsync_WithUltimateCpId_DetectsF5()
    {
        var response = new HttpResponseData
        {
            StatusCode = 200,
            Headers = new Dictionary<string, string>
            {
                { "x-ultimate-cp-id", "abc123" }
            }
        };

        var evidence = await _provider.PassiveDetectAsync(response);
        evidence.Should().Contain(e => e.Signature == "x-ultimate-cp-id-header");
    }

    [Fact]
    public async Task DetectAsync_WithBigipServer_DetectsServer()
    {
        var response = new HttpResponseData
        {
            StatusCode = 200,
            Headers = new Dictionary<string, string>
            {
                { "server", "bigip" }
            }
        };

        var evidence = await _provider.PassiveDetectAsync(response);
        evidence.Should().Contain(e => e.Signature == "f5-bigip-header");
    }

    [Fact]
    public async Task DetectAsync_WithF5Cookie_DetectsCookie()
    {
        var response = new HttpResponseData
        {
            StatusCode = 200,
            Headers = new Dictionary<string, string>
            {
                { "set-cookie", "F5_TM_COOKIE=abc123" }
            }
        };

        var evidence = await _provider.PassiveDetectAsync(response);
        evidence.Should().Contain(e => e.Signature == "f5-bigip-cookie");
    }

    [Fact]
    public async Task DetectAsync_NoF5Headers_ReturnsEmpty()
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
        _provider.Name.Should().Be("F5");
        _provider.ProviderType.Should().Be(ProviderType.WAF);
        _provider.Priority.Should().Be(65);
    }
}
