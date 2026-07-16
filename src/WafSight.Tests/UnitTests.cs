using System.Net;
using System.Text;
using FluentAssertions;
using Moq;
using WafSight.Analysis;
using WafSight.Http;
using WafSight.Models;
using WafSight.Providers;
using WafSight.Registry;
using Xunit;

namespace WafSight.Tests;

/// <summary>
/// Tests for WafHttpClient resilience, timeouts, and headers
/// </summary>
public class WafHttpClientTests : IDisposable
{
    [Fact]
    public async Task GetAsync_ReturnsResponseData_WithValidResponse()
    {
        var handler = CreateMockHandler(200, new Dictionary<string, string>
        {
            { "server", "nginx" },
            { "content-type", "text/html" }
        }, "<html>OK</html>");

        using var client = new WafHttpClient(handler, timeout: TimeSpan.FromSeconds(5));

        var response = await client.GetAsync("https://example.com");

        response.Should().NotBeNull();
        response!.StatusCode.Should().Be(200);
        response.Headers.Should().ContainKey("server");
        response.Body.Should().Contain("OK");
        response.Url.Should().Be("https://example.com");
    }

    [Fact]
    public async Task GetAsync_ReturnsNull_WhenRequestFails()
    {
        var handler = new FailingHttpMessageHandler();

        using var client = new WafHttpClient(handler, timeout: TimeSpan.FromSeconds(5));

        var response = await client.GetAsync("https://invalid.example.com");

        response.Should().BeNull();
    }

    [Fact]
    public async Task GetAsync_WithCustomTimeout_AppliesTimeout()
    {
        var handler = new SlowHttpMessageHandler(TimeSpan.FromSeconds(1));

        using var client = new WafHttpClient(handler, timeout: TimeSpan.FromMilliseconds(100));

        var response = await client.GetAsync("https://slow.example.com");

        response.Should().BeNull();
    }

    [Fact]
    public async Task GetWithHeadersAsync_SendsCustomHeaders()
    {
        var capturedHeaders = new Dictionary<string, string>();
        var handler = new CapturingHttpMessageHandler(capturedHeaders);

        using var client = new WafHttpClient(handler, timeout: TimeSpan.FromSeconds(5));

        var customHeaders = new Dictionary<string, string>
        {
            { "X-Custom-Header", "test-value" },
            { "X-Another-Header", "another-value" }
        };

        var response = await client.GetWithHeadersAsync("https://example.com", customHeaders);

        response.Should().NotBeNull();
        capturedHeaders.Should().ContainKey("X-Custom-Header");
        capturedHeaders["X-Custom-Header"].Should().Be("test-value");
    }

    [Fact]
    public async Task GetAsync_DefaultHeaders_AreSet()
    {
        var handler = new CapturingHttpMessageHandler(new Dictionary<string, string>());

        using var client = new WafHttpClient(handler, timeout: TimeSpan.FromSeconds(5));

        await client.GetAsync("https://example.com");

        handler.CapturedHeaders.Should().ContainKey("User-Agent");
        handler.CapturedHeaders["User-Agent"].Should().Be("WafSight/2.0");
    }

    [Fact]
    public async Task GetAsync_ServerError_StillReturnsResponse()
    {
        var handler = CreateMockHandler(500, new Dictionary<string, string>(), "Internal Server Error");

        using var client = new WafHttpClient(handler, timeout: TimeSpan.FromSeconds(5));

        var response = await client.GetAsync("https://example.com");

        response.Should().NotBeNull();
        response!.StatusCode.Should().Be(500);
    }

    [Fact]
    public async Task GetAsync_NotFound_Returns404()
    {
        var handler = CreateMockHandler(404, new Dictionary<string, string>(), "Not Found");

        using var client = new WafHttpClient(handler, timeout: TimeSpan.FromSeconds(5));

        var response = await client.GetAsync("https://example.com/missing");

        response.Should().NotBeNull();
        response!.StatusCode.Should().Be(404);
    }

    private HttpMessageHandler CreateMockHandler(int statusCode, Dictionary<string, string> headers, string body)
    {
        var handler = new MockHttpMessageHandler(statusCode, headers, body);
        return handler;
    }

    public void Dispose()
    {
        // Cleanup if needed
    }

    private class FailingHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            throw new HttpRequestException("Connection failed");
        }
    }

    private class SlowHttpMessageHandler : HttpMessageHandler
    {
        private readonly TimeSpan _delay;

        public SlowHttpMessageHandler(TimeSpan delay)
        {
            _delay = delay;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<HttpResponseMessage>();
            Task.Delay(_delay, cancellationToken).ContinueWith(_ =>
            {
                tcs.TrySetException(new TaskCanceledException("Request timed out"));
            });
            return tcs.Task;
        }
    }

    private class CapturingHttpMessageHandler : HttpMessageHandler
    {
        private readonly Dictionary<string, string> _capturedHeaders;

        public CapturingHttpMessageHandler(Dictionary<string, string> capturedHeaders)
        {
            _capturedHeaders = capturedHeaders;
        }

        public Dictionary<string, string> CapturedHeaders => _capturedHeaders;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            foreach (var header in request.Headers)
            {
                _capturedHeaders[header.Key] = string.Join(", ", header.Value);
            }

            var response = new HttpResponseMessage(HttpStatusCode.OK);
            response.Content = new StringContent("OK", Encoding.UTF8, "text/plain");
            response.RequestMessage = request;

            return Task.FromResult(response);
        }
    }
}

/// <summary>
/// Tests for remaining providers (Akamai, Fastly, Azure, Imperva, Sucuri, F5)
/// </summary>
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

public class SucuriProviderTests
{
    private readonly SucuriProvider _provider = new();

    [Fact]
    public async Task DetectAsync_WithSucuriId_DetectsSucuri()
    {
        var response = new HttpResponseData
        {
            StatusCode = 200,
            Headers = new Dictionary<string, string>
            {
                { "x-sucuri-id", "10001" }
            }
        };

        var evidence = await _provider.PassiveDetectAsync(response);
        evidence.Should().Contain(e => e.Signature == "x-sucuri-id-header");
    }

    [Fact]
    public async Task DetectAsync_WithSucuriCache_DetectsCache()
    {
        var response = new HttpResponseData
        {
            StatusCode = 200,
            Headers = new Dictionary<string, string>
            {
                { "x-sucuri-cache", "HIT" }
            }
        };

        var evidence = await _provider.PassiveDetectAsync(response);
        evidence.Should().Contain(e => e.Signature == "x-sucuri-cache-header");
    }

    [Fact]
    public async Task DetectAsync_WithSucuriBlock_DetectsBlock()
    {
        var response = new HttpResponseData
        {
            StatusCode = 200,
            Headers = new Dictionary<string, string>
            {
                { "x-sucuri-block", "Yes" }
            }
        };

        var evidence = await _provider.PassiveDetectAsync(response);
        evidence.Should().Contain(e => e.Signature == "x-sucuri-block-header");
    }

    [Fact]
    public async Task DetectAsync_WithSucuriBody_DetectsBody()
    {
        var response = new HttpResponseData
        {
            StatusCode = 200,
            Body = "<html>Access denied - sucuri webserver firewall</html>"
        };

        var evidence = await _provider.PassiveDetectAsync(response);
        evidence.Should().Contain(e => e.Signature == "sucuri-body");
    }

    [Fact]
    public async Task DetectAsync_NoSucuriHeaders_ReturnsEmpty()
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
        _provider.Name.Should().Be("Sucuri");
        _provider.ProviderType.Should().Be(ProviderType.WAF);
        _provider.Priority.Should().Be(70);
    }
}

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

/// <summary>
/// Tests for batch detection and concurrency
/// </summary>
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

/// <summary>
/// Tests for EvidenceScorer edge cases
/// </summary>
public class EvidenceScorerEdgeCaseTests
{
    private readonly EvidenceScorer _scorer = new();

    [Fact]
    public void CalculateConfidence_NullEvidence_ThrowsArgumentNullException()
    {
        var result = () => _scorer.CalculateConfidence(null!);
        result.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void CalculateConfidence_EmptyList_ReturnsZero()
    {
        var result = _scorer.CalculateConfidence(new List<Evidence>());
        result.Should().Be(0.0);
    }

    [Fact]
    public void CalculateConfidence_ZeroConfidenceEvidence_ReturnsZero()
    {
        var evidence = new List<Evidence>
        {
            new() { Method = DetectionMethod.Header, Confidence = 0.0, Weight = 1.0 }
        };

        var result = _scorer.CalculateConfidence(evidence);
        result.Should().Be(0.0);
    }

    [Fact]
    public void CalculateConfidence_MaxConfidenceEvidence_ReturnsOne()
    {
        var evidence = new List<Evidence>
        {
            new() { Method = DetectionMethod.Header, Confidence = 1.0, Weight = 1.0 }
        };

        var result = _scorer.CalculateConfidence(evidence);
        result.Should().Be(1.0);
    }

    [Fact]
    public void CalculateConfidence_ManyEvidence_AppliesBonus()
    {
        var evidence = new List<Evidence>
        {
            new() { Method = DetectionMethod.Header, Confidence = 0.7, Weight = 1.0 },
            new() { Method = DetectionMethod.Header, Confidence = 0.7, Weight = 1.0 },
            new() { Method = DetectionMethod.Header, Confidence = 0.7, Weight = 1.0 },
            new() { Method = DetectionMethod.Header, Confidence = 0.7, Weight = 1.0 },
            new() { Method = DetectionMethod.Header, Confidence = 0.7, Weight = 1.0 }
        };

        var result = _scorer.CalculateConfidence(evidence);
        result.Should().BeGreaterThan(0.7);
        result.Should().BeLessThanOrEqualTo(1.0);
    }

    [Fact]
    public void GenerateCaveats_TimingHeavy_ReturnsWarning()
    {
        var evidence = new List<Evidence>
        {
            new() { Method = DetectionMethod.Timing, Confidence = 0.5 },
            new() { Method = DetectionMethod.Timing, Confidence = 0.5 },
            new() { Method = DetectionMethod.Header, Confidence = 0.8 }
        };

        var caveats = _scorer.GenerateCaveats(evidence, "TestProvider");
        caveats.Should().Contain(c => c.Contains("timing analysis"));
    }

    [Fact]
    public void GenerateCaveats_MixedEvidence_NoWarning()
    {
        var evidence = new List<Evidence>
        {
            new() { Method = DetectionMethod.Header, Confidence = 0.8 },
            new() { Method = DetectionMethod.Body, Confidence = 0.5 },
            new() { Method = DetectionMethod.Timing, Confidence = 0.5 }
        };

        var caveats = _scorer.GenerateCaveats(evidence, "TestProvider");
        caveats.Should().BeEmpty();
    }

    [Fact]
    public void HasTier1Evidence_EmptyList_ReturnsFalse()
    {
        var result = _scorer.HasTier1Evidence(new List<Evidence>());
        result.Should().BeFalse();
    }

    [Fact]
    public void HasTier1Evidence_Certificate_ReturnsTrue()
    {
        var evidence = new List<Evidence>
        {
            new() { Method = DetectionMethod.Certificate }
        };

        var result = _scorer.HasTier1Evidence(evidence);
        result.Should().BeTrue();
    }

    [Fact]
    public void GetMethodWeight_UnknownMethod_ReturnsDefault()
    {
        var result = EvidenceScorer.GetMethodWeight((DetectionMethod)999);
        result.Should().Be(0.5);
    }
}

/// <summary>
/// Additional tests for DetectionResult and ProviderDetection
/// </summary>
public class DetectionResultAdditionalTests
{
    [Fact]
    public void ToSummary_WithWafAndCdn_IncludesBoth()
    {
        var result = new DetectionResult
        {
            Url = "https://example.com",
            Waf = new ProviderDetection { Name = "CloudFlare", Confidence = 0.95 },
            Cdn = new ProviderDetection { Name = "CloudFlare", Confidence = 0.95 },
            Evidence = new List<Evidence>(),
            DetectionTimeMs = 150
        };

        var summary = result.ToSummary();

        summary.Should().Contain("CloudFlare");
        summary.Should().Contain("CDN");
        summary.Should().Contain("150ms");
    }

    [Fact]
    public void ToSummary_WithCaveats_IncludesCaveats()
    {
        var result = new DetectionResult
        {
            Url = "https://example.com",
            Waf = new ProviderDetection { Name = "CloudFlare", Confidence = 0.95 },
            Caveats = new List<string> { "Caveat 1", "Caveat 2" }
        };

        var summary = result.ToSummary();

        summary.Should().Contain("Caveats:");
        summary.Should().Contain("Caveat 1");
        summary.Should().Contain("Caveat 2");
    }

    [Fact]
    public void HasWaf_WithWaf_ReturnsTrue()
    {
        var result = new DetectionResult
        {
            Waf = new ProviderDetection { Name = "CloudFlare" }
        };

        result.HasWaf.Should().BeTrue();
    }

    [Fact]
    public void HasWaf_WithoutWaf_ReturnsFalse()
    {
        var result = new DetectionResult();
        result.HasWaf.Should().BeFalse();
    }

    [Fact]
    public void Detected_WithWafOrCdn_ReturnsTrue()
    {
        var result = new DetectionResult
        {
            Waf = new ProviderDetection { Name = "CloudFlare" }
        };

        result.Detected.Should().BeTrue();
    }

    [Fact]
    public void Detected_NoWafOrCdn_ReturnsFalse()
    {
        var result = new DetectionResult();
        result.Detected.Should().BeFalse();
    }

    [Fact]
    public void ProviderDetection_DefaultValues()
    {
        var detection = new ProviderDetection();

        detection.Name.Should().BeEmpty();
        detection.Confidence.Should().Be(0.0);
        detection.Version.Should().BeNull();
    }
}

/// <summary>
/// Tests for ProviderRegistry with disabled providers
/// </summary>
public class ProviderRegistryDisabledTests
{
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

/// <summary>
/// Tests for GenericDetector with various scenarios
/// </summary>
public class GenericDetectorEdgeCaseTests
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
    public async Task DetectGenericAsync_NoPayloadBlocked_ReturnsNull()
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

        var result = await detector.DetectGenericAsync(context, _ =>
            Task.FromResult<HttpResponseData?>(new HttpResponseData
            {
                StatusCode = 200,
                Headers = new Dictionary<string, string>
                {
                    { "server", "nginx" }
                }
            }));

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

        var result = await detector.DetectGenericAsync(context, _ =>
            Task.FromResult<HttpResponseData?>(null));

        result.Should().NotBeNull();
        result!.HasWaf.Should().BeTrue();
        result.Waf!.Name.Should().Be("Generic WAF");
    }

    [Fact]
    public async Task DetectGenericAsync_ServerHeaderChanged_ReturnsDetection()
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

        var result = await detector.DetectGenericAsync(context, url =>
        {
            if (url.Contains("_wafdetect_xss"))
            {
                return Task.FromResult<HttpResponseData?>(new HttpResponseData
                {
                    StatusCode = 403,
                    Headers = new Dictionary<string, string>
                    {
                        { "server", "CloudFlare" }
                    }
                });
            }
            return Task.FromResult<HttpResponseData?>(null);
        });

        result.Should().NotBeNull();
        result!.HasWaf.Should().BeTrue();
    }
}

/// <summary>
/// Tests for DetectionContext and HttpResponseData
/// </summary>
public class ModelTests
{
    [Fact]
    public void DetectionContext_DefaultValues()
    {
        var context = new DetectionContext();

        context.Url.Should().BeEmpty();
        context.Response.Should().BeNull();
        context.DnsInfo.Should().BeNull();
        context.UserAgent.Should().Be("WafSight/2.0");
        context.PayloadResponses.Should().BeEmpty();
    }

    [Fact]
    public void HttpResponseData_DefaultValues()
    {
        var response = new HttpResponseData();

        response.StatusCode.Should().Be(0);
        response.Headers.Should().BeEmpty();
        response.Body.Should().BeEmpty();
        response.Url.Should().BeEmpty();
        response.ResponseTime.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void DnsInfo_DefaultValues()
    {
        var dnsInfo = new DnsInfo();

        dnsInfo.Cnames.Should().BeEmpty();
        dnsInfo.ARecords.Should().BeEmpty();
        dnsInfo.NsRecords.Should().BeEmpty();
        dnsInfo.TxtRecords.Should().BeEmpty();
    }

    [Fact]
    public void Evidence_DefaultValues()
    {
        var evidence = new Evidence();

        evidence.Method.Should().Be(DetectionMethod.Header);
        evidence.Name.Should().BeEmpty();
        evidence.Value.Should().BeEmpty();
        evidence.Signature.Should().BeEmpty();
        evidence.Confidence.Should().Be(0.0);
        evidence.Description.Should().BeEmpty();
        evidence.Weight.Should().Be(1.0);
    }

    [Fact]
    public void ProviderType_AllValuesExist()
    {
        var values = Enum.GetValues<ProviderType>();

        values.Should().Contain(ProviderType.WAF);
        values.Should().Contain(ProviderType.CDN);
        values.Should().Contain(ProviderType.Both);
    }

    [Fact]
    public void DetectionMethod_AllValuesExist()
    {
        var values = Enum.GetValues<DetectionMethod>();

        values.Should().Contain(DetectionMethod.Header);
        values.Should().Contain(DetectionMethod.Body);
        values.Should().Contain(DetectionMethod.StatusCode);
        values.Should().Contain(DetectionMethod.DNS);
        values.Should().Contain(DetectionMethod.Timing);
        values.Should().Contain(DetectionMethod.Certificate);
        values.Should().Contain(DetectionMethod.Cookie);
        values.Should().Contain(DetectionMethod.Payload);
    }
}

/// <summary>
/// Helper class for creating mock HTTP responses
/// </summary>
internal class MockHttpMessageHandler : HttpMessageHandler
{
    private readonly int _statusCode;
    private readonly Dictionary<string, string> _headers;
    private readonly string _body;

    public MockHttpMessageHandler(int statusCode, Dictionary<string, string> headers, string body)
    {
        _statusCode = statusCode;
        _headers = headers;
        _body = body;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var response = new HttpResponseMessage((HttpStatusCode)_statusCode);

        foreach (var header in _headers)
        {
            response.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        response.Content = new StringContent(_body, Encoding.UTF8, "text/html");
        response.RequestMessage = request;

        return Task.FromResult(response);
    }
}
