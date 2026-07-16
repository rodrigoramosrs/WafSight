using FluentAssertions;
using WafSight.Models;
using WafSight.Providers;
using Xunit;

namespace WafSight.Tests.Tests;

public class AwsProviderTests
{
    private readonly AwsProvider _provider = new();

    [Fact]
    public async Task DetectAsync_WithXAmzCfId_DetectsCloudFront()
    {
        var response = new HttpResponseData
        {
            StatusCode = 200,
            Headers = new Dictionary<string, string>
            {
                { "x-amz-cf-id", "abcdefgh-1234-5678-abcd-123456789abc" }
            }
        };

        var evidence = await _provider.PassiveDetectAsync(response);
        evidence.Should().Contain(e => e.Signature == "x-amz-cf-id-header");
    }

    [Fact]
    public async Task DetectAsync_WithXAmzCfPop_DetectsCloudFront()
    {
        var response = new HttpResponseData
        {
            StatusCode = 200,
            Headers = new Dictionary<string, string>
            {
                { "x-amz-cf-pop", "NRT51-P1" }
            }
        };

        var evidence = await _provider.PassiveDetectAsync(response);
        evidence.Should().Contain(e => e.Signature == "x-amz-cf-pop-header");
    }

    [Fact]
    public async Task DetectAsync_WithNoAwsHeaders_ReturnsEmptyEvidence()
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
}
