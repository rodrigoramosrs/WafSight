using FluentAssertions;
using WafSight.Analysis;
using WafSight.Models;
using WafSight.Providers;
using WafSight.Registry;
using WafSight.Http;
using Xunit;

namespace WafSight.Tests;

public class EvidenceScorerTests
{
    private readonly EvidenceScorer _scorer = new();

    [Fact]
    public void CalculateConfidence_EmptyEvidence_ReturnsZero()
    {
        var result = _scorer.CalculateConfidence(Array.Empty<Evidence>());
        result.Should().Be(0.0);
    }

    [Fact]
    public void CalculateConfidence_SingleHighConfidenceHeader_ReturnsHighScore()
    {
        var evidence = new List<Evidence>
        {
            new() { Method = DetectionMethod.Header, Confidence = 0.95, Weight = 1.0 }
        };

        var result = _scorer.CalculateConfidence(evidence);
        result.Should().BeGreaterOrEqualTo(0.95);
    }

    [Fact]
    public void CalculateConfidence_MultipleEvidence_AppliesBonus()
    {
        var evidence = new List<Evidence>
        {
            new() { Method = DetectionMethod.Header, Confidence = 0.8, Weight = 1.0 },
            new() { Method = DetectionMethod.Cookie, Confidence = 0.8, Weight = 1.0 },
            new() { Method = DetectionMethod.Header, Confidence = 0.8, Weight = 1.0 }
        };

        var result = _scorer.CalculateConfidence(evidence);
        result.Should().BeGreaterThan(0.8);
    }

    [Fact]
    public void CalculateConfidence_LowConfidenceBody_Evidence_ReturnsLowScore()
    {
        var evidence = new List<Evidence>
        {
            new() { Method = DetectionMethod.Body, Confidence = 0.5, Weight = 1.0 }
        };

        var result = _scorer.CalculateConfidence(evidence);
        result.Should().BeLessThanOrEqualTo(0.5);
    }

    [Fact]
    public void HasTier1Evidence_Header_ReturnsTrue()
    {
        var evidence = new List<Evidence>
        {
            new() { Method = DetectionMethod.Header }
        };

        _scorer.HasTier1Evidence(evidence).Should().BeTrue();
    }

    [Fact]
    public void HasTier1Evidence_BodyOnly_ReturnsFalse()
    {
        var evidence = new List<Evidence>
        {
            new() { Method = DetectionMethod.Body }
        };

        _scorer.HasTier1Evidence(evidence).Should().BeFalse();
    }

    [Fact]
    public void GetMethodWeight_ValidMethod_ReturnsCorrectWeight()
    {
        EvidenceScorer.GetMethodWeight(DetectionMethod.Header).Should().Be(1.0);
        EvidenceScorer.GetMethodWeight(DetectionMethod.DNS).Should().Be(0.95);
        EvidenceScorer.GetMethodWeight(DetectionMethod.Payload).Should().Be(0.40);
    }

    [Fact]
    public void GenerateCaveats_BodyOnly_NoHeader_ReturnsCaveat()
    {
        var evidence = new List<Evidence>
        {
            new() { Method = DetectionMethod.Body, Confidence = 0.5 }
        };

        var caveats = _scorer.GenerateCaveats(evidence, "TestProvider");
        caveats.Should().ContainSingle();
        caveats[0].Should().Contain("body patterns");
    }
}

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
}

public class DetectionResultTests
{
    [Fact]
    public void ToSummary_WithDetection_IncludesDetails()
    {
        var result = new DetectionResult
        {
            Url = "https://example.com",
            Waf = new ProviderDetection { Name = "CloudFlare", Confidence = 0.95 },
            Cdn = new ProviderDetection { Name = "CloudFlare", Confidence = 0.95 },
            Evidence = new List<Evidence>
            {
                new() { Method = DetectionMethod.Header, Name = "cf-ray", Value = "abc" }
            },
            DetectionTimeMs = 150
        };

        var summary = result.ToSummary();

        summary.Should().Contain("https://example.com");
        summary.Should().Contain("CloudFlare");
        summary.Should().Contain("1");
        summary.Should().Contain("150ms");
    }

    [Fact]
    public void ToSummary_NoDetection_ShowsNotDetected()
    {
        var result = new DetectionResult
        {
            Url = "https://example.com",
            DetectionTimeMs = 100
        };

        var summary = result.ToSummary();

        summary.Should().Contain("Not detected");
    }
}

public class GenericDetectorTests
{
    [Fact]
    public async Task DetectGenericAsync_NullResponse_ReturnsNull()
    {
        var detector = new GenericDetector();
        var context = new DetectionContext
        {
            Url = "https://example.com",
            Response = null
        };

        var result = await detector.DetectGenericAsync(context, _ => Task.FromResult<HttpResponseData?>(null));
        result.Should().BeNull();
    }

    [Fact]
    public async Task DetectGenericAsync_ConnectionBlocked_ReturnsGenericDetection()
    {
        var detector = new GenericDetector();
        var context = new DetectionContext
        {
            Url = "https://example.com",
            Response = new HttpResponseData
            {
                StatusCode = 200,
                Headers = new Dictionary<string, string>
                {
                    { "server", "nginx" }
                }
            }
        };

        var result = await detector.DetectGenericAsync(context, _ => Task.FromResult<HttpResponseData?>(null));
        result.Should().NotBeNull();
        result!.HasWaf.Should().BeTrue();
        result.Waf!.Name.Should().Be("Generic WAF");
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

public class DnsAnalyzerTests
{
    [Fact]
    public async Task ResolveAsync_ValidUrl_ReturnsDnsInfo()
    {
        var analyzer = new DnsAnalyzer();

        var result = await analyzer.ResolveAsync("http://example.com");

        result.Should().NotBeNull();
        result!.ARecords.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ResolveAsync_EmptyHost_ReturnsNull()
    {
        var analyzer = new DnsAnalyzer();

        var result = await analyzer.ResolveAsync("not-a-url");

        result.Should().BeNull();
    }
}
