using FluentAssertions;
using WafSight.Models;
using WafSight.Providers;
using Xunit;

namespace WafSight.Tests.Tests;

public class AzureProviderTests
{
    private readonly AzureProvider _provider = new();

    [Fact]
    public async Task DetectAsync_WithAzureRef_DetectsAzure()
    {
        var response = new HttpResponseData
        {
            StatusCode = 200,
            Headers = new Dictionary<string, string>
            {
                { "x-azure-ref", "20240101T000000Z-abc123" }
            }
        };

        var evidence = await _provider.PassiveDetectAsync(response);
        evidence.Should().Contain(e => e.Signature == "x-azure-ref-header");
    }

    [Fact]
    public async Task DetectAsync_WithArrLogId_DetectsAzure()
    {
        var response = new HttpResponseData
        {
            StatusCode = 200,
            Headers = new Dictionary<string, string>
            {
                { "x-arr-log-id", "abc123-def456" }
            }
        };

        var evidence = await _provider.PassiveDetectAsync(response);
        evidence.Should().Contain(e => e.Signature == "x-arr-log-id-header");
    }

    [Fact]
    public async Task DetectAsync_NoAzureHeaders_ReturnsEmpty()
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
        _provider.Name.Should().Be("Azure");
        _provider.ProviderType.Should().Be(ProviderType.Both);
        _provider.Priority.Should().Be(80);
    }
}
