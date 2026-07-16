using FluentAssertions;
using WafSight.Models;
using WafSight.Providers;
using WafSight.Registry;
using Xunit;

namespace WafSight.Tests.Unit;

public class ProviderRegistryTests
{
    [Fact]
    public void RegisterProvider_DuplicateName_ThrowsException()
    {
        var registry = new ProviderRegistry();
        var provider = new CloudFlareProvider();

        registry.RegisterProvider(provider);
        Action act = () => registry.RegisterProvider(provider);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*already registered*");
    }

    [Fact]
    public void ListProviders_ReturnsSortedByPriority()
    {
        var registry = new ProviderRegistry();
        registry.RegisterProvider(new CloudFlareProvider());
        registry.RegisterProvider(new AwsProvider());
        registry.RegisterProvider(new AzureProvider());

        var providers = registry.ListProviders();
        providers[0].Name.Should().Be("CloudFlare");
        providers[1].Name.Should().Be("AWS");
        providers[2].Name.Should().Be("Azure");
    }

    [Fact]
    public void GetProviderCount_ReturnsCorrectCount()
    {
        var registry = new ProviderRegistry();
        registry.GetProviderCount().Should().Be(0);

        registry.RegisterProvider(new CloudFlareProvider());
        registry.GetProviderCount().Should().Be(1);

        registry.RegisterProvider(new AwsProvider());
        registry.GetProviderCount().Should().Be(2);
    }

    [Fact]
    public async Task DetectAllAsync_WithCloudFlareResponse_DetectsWafAndCdn()
    {
        var registry = new ProviderRegistry();
        registry.RegisterProvider(new CloudFlareProvider());

        var context = new DetectionContext
        {
            Url = "https://example.com",
            Response = new HttpResponseData
            {
                StatusCode = 200,
                Headers = new Dictionary<string, string>
                {
                    { "cf-ray", "abc123-CDG" },
                    { "cf-cache-status", "HIT" },
                    { "server", "cloudflare" }
                },
                Body = "OK"
            }
        };

        var result = await registry.DetectAllAsync(context);

        result.HasWaf.Should().BeTrue();
        result.HasCdn.Should().BeTrue();
        result.Waf!.Name.Should().Be("CloudFlare");
        result.Cdn!.Name.Should().Be("CloudFlare");
        result.Waf.Confidence.Should().BeGreaterOrEqualTo(0.60);
    }

    [Fact]
    public async Task DetectAllAsync_WithNoMatchingResponse_ReturnsNoDetection()
    {
        var registry = new ProviderRegistry();
        registry.RegisterProvider(new CloudFlareProvider());

        var context = new DetectionContext
        {
            Url = "https://example.com",
            Response = new HttpResponseData
            {
                StatusCode = 200,
                Headers = new Dictionary<string, string>
                {
                    { "server", "nginx/1.18.0" }
                },
                Body = "OK"
            }
        };

        var result = await registry.DetectAllAsync(context);

        result.HasWaf.Should().BeFalse();
        result.HasCdn.Should().BeFalse();
        result.Detected.Should().BeFalse();
    }

    [Fact]
    public async Task DetectAllAsync_DisabledProvider_IsNotDetected()
    {
        var registry = new ProviderRegistry();
        var disabledProvider = new DisabledCloudFlareProvider();

        registry.RegisterProvider(disabledProvider);

        var context = new DetectionContext
        {
            Url = "https://example.com",
            Response = new HttpResponseData
            {
                StatusCode = 200,
                Headers = new Dictionary<string, string>
                {
                    { "cf-ray", "abc123-CDG" }
                }
            }
        };

        var result = await registry.DetectAllAsync(context);

        result.HasWaf.Should().BeFalse();
        result.HasCdn.Should().BeFalse();
    }

    [Fact]
    public void ListProviders_DisabledProvider_NotIncluded()
    {
        var registry = new ProviderRegistry();
        registry.RegisterProvider(new DisabledCloudFlareProvider());

        var providers = registry.ListProviders();
        providers.Should().Contain(p => p.Name == "CloudFlare");
        providers.Should().Contain(p => p.Enabled == false);
    }

    private class DisabledCloudFlareProvider : IDetectionProvider
    {
        public string Name => "CloudFlare";
        public string Version => "1.0.0";
        public string Description => "Disabled CloudFlare for testing";
        public ProviderType ProviderType => ProviderType.Both;
        public double ConfidenceBase => 0.95;
        public int Priority => 100;
        public bool Enabled => false;

        public Task<List<Evidence>> DetectAsync(DetectionContext context)
        {
            return Task.FromResult(new List<Evidence>());
        }

        public Task<List<Evidence>> PassiveDetectAsync(HttpResponseData response)
        {
            return Task.FromResult(new List<Evidence>());
        }
    }
}
