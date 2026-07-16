using FluentAssertions;
using WafSight;
using WafSight.Models;
using WafSight.Providers;
using Xunit;

namespace WafSight.Tests.Unit;

public class WafDetectorClientBatchTests : IDisposable
{
    private readonly WafDetectorClient _client;

    public WafDetectorClientBatchTests()
    {
        _client = new WafDetectorClient();
    }

    [Fact]
    public async Task DetectBatchAsync_MultipleUrls_ReturnsAllResults()
    {
        var urls = new[]
        {
            "https://cf1.example.com",
            "https://cf2.example.com",
            "https://cf3.example.com"
        };

        var results = await _client.DetectBatchAsync(urls, maxConcurrency: 2);

        results.Should().HaveCount(3);
        results.Keys.Should().AllSatisfy(url => url.Should().NotBeEmpty());
    }

    [Fact]
    public async Task DetectBatchAsync_EmptyUrls_ReturnsEmptyDictionary()
    {
        var results = await _client.DetectBatchAsync(Array.Empty<string>());

        results.Should().BeEmpty();
    }

    [Fact]
    public async Task DetectBatchAsync_WithException_HandledGracefully()
    {
        var urls = new[] { "https://invalid1.example.com", "https://invalid2.example.com" };

        var results = await _client.DetectBatchAsync(urls);

        results.Should().HaveCount(2);
        foreach (var result in results.Values)
        {
            result.DetectionTimeMs.Should().BeGreaterOrEqualTo(0);
        }
    }

    public void Dispose()
    {
        _client?.Dispose();
    }
}

public class WafDetectorClientTests
{
    [Fact]
    public void Constructor_RegistersDefaultProviders()
    {
        using var client = new WafDetectorClient();

        client.GetProviderCount().Should().BeGreaterOrEqualTo(8);
        client.ListProviders().Should().NotBeEmpty();
    }

    [Fact]
    public void ListProviders_ReturnsProvidersSortedByPriority()
    {
        using var client = new WafDetectorClient();

        var providers = client.ListProviders();
        for (int i = 1; i < providers.Count; i++)
        {
            providers[i].Priority.Should().BeLessThanOrEqualTo(providers[i - 1].Priority);
        }
    }

    [Fact]
    public void RegisterProvider_AddsCustomProvider()
    {
        using var client = new WafDetectorClient();
        var initialCount = client.GetProviderCount();

        var customProvider = new CustomTestProvider();
        client.RegisterProvider(customProvider);

        client.GetProviderCount().Should().Be(initialCount + 1);
    }

    private class CustomTestProvider : IDetectionProvider
    {
        public string Name => "CustomTest";
        public string Version => "1.0.0";
        public string Description => "Custom test provider";
        public ProviderType ProviderType => ProviderType.WAF;
        public double ConfidenceBase => 0.8;
        public int Priority => 10;
        public bool Enabled => true;

        public Task<List<Evidence>> DetectAsync(DetectionContext context) => Task.FromResult(new List<Evidence>());
        public Task<List<Evidence>> PassiveDetectAsync(HttpResponseData response) => Task.FromResult(new List<Evidence>());
    }
}
