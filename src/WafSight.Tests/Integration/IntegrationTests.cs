using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using WafSight.Analysis;
using WafSight.Extensions;
using WafSight.Http;
using WafSight.Models;
using WafSight.Providers;
using WafSight.Registry;
using Xunit;

namespace WafSight.Tests.Integration;

public class WafDetectorClientIntegrationTests : IDisposable
{
    private readonly WafDetectorClient _client;

    public WafDetectorClientIntegrationTests()
    {
        _client = new WafDetectorClient();
    }

    [Fact]
    public async Task DetectAsync_CloudFlareHeaders_ReturnsCloudFlareDetection()
    {
        var handler = CreateMockHandler(
            statusCode: 200,
            headers: new Dictionary<string, string>
            {
                { "cf-ray", "abc123-CDG" },
                { "cf-cache-status", "HIT" },
                { "server", "cloudflare" },
                { "set-cookie", "__cfduid=abc123; __cf_bm=def456" }
            },
            body: "<html>OK</html>");

        using var httpClient = CreateHttpClient(handler);
        var dnsAnalyzer = new DnsAnalyzer();

        var response = await httpClient.GetAsync("https://example.com");

        var context = new DetectionContext
        {
            Url = "https://example.com",
            Response = response,
            DnsInfo = await dnsAnalyzer.ResolveAsync("https://example.com")
        };

        var registry = new ProviderRegistry();
        registry.RegisterProvider(new CloudFlareProvider());

        var result = await registry.DetectAllAsync(context);

        result.HasWaf.Should().BeTrue();
        result.HasCdn.Should().BeTrue();
        result.Waf!.Name.Should().Be("CloudFlare");
        result.Waf.Confidence.Should().BeGreaterOrEqualTo(0.80);
        result.Cdn!.Name.Should().Be("CloudFlare");
        result.Evidence.Should().HaveCountGreaterOrEqualTo(3);
        result.DetectionTimeMs.Should().BeGreaterOrEqualTo(0);
    }

    [Fact]
    public async Task DetectAsync_AwsCloudFront_ReturnsAwsDetection()
    {
        var handler = CreateMockHandler(
            statusCode: 200,
            headers: new Dictionary<string, string>
            {
                { "x-amz-cf-id", "abcdefgh-1234-5678-abcd-123456789abc" },
                { "x-amz-cf-pop", "NRT51-P1" },
                { "via", "1.1 varnish (CloudFront)" }
            },
            body: "<html>OK</html>");

        using var httpClient = CreateHttpClient(handler);
        var response = await httpClient.GetAsync("https://cdn.example.com");

        var context = new DetectionContext
        {
            Url = "https://cdn.example.com",
            Response = response
        };

        var registry = new ProviderRegistry();
        registry.RegisterProvider(new AwsProvider());

        var result = await registry.DetectAllAsync(context);

        result.HasCdn.Should().BeTrue();
        result.Cdn!.Name.Should().Be("AWS");
        result.Evidence.Should().Contain(e => e.Signature == "x-amz-cf-id-header");
        result.Evidence.Should().Contain(e => e.Signature == "x-amz-cf-pop-header");
    }

    [Fact]
    public async Task DetectAsync_Akamai_ReturnsAkamaiDetection()
    {
        var handler = CreateMockHandler(
            statusCode: 200,
            headers: new Dictionary<string, string>
            {
                { "x-akamai-transformed", "9 -" },
                { "server", "AkamaiG2" },
                { "via", "Akamai-G2" }
            },
            body: "<html>OK</html>");

        using var httpClient = CreateHttpClient(handler);
        var response = await httpClient.GetAsync("https://akamai.example.com");

        var context = new DetectionContext
        {
            Url = "https://akamai.example.com",
            Response = response
        };

        var registry = new ProviderRegistry();
        registry.RegisterProvider(new AkamaiProvider());

        var result = await registry.DetectAllAsync(context);

        result.HasCdn.Should().BeTrue();
        result.Cdn!.Name.Should().Be("Akamai");
        result.Evidence.Should().Contain(e => e.Signature == "x-akamai-transformed-header");
    }

    [Fact]
    public async Task DetectAsync_Fastly_ReturnsFastlyDetection()
    {
        var handler = CreateMockHandler(
            statusCode: 200,
            headers: new Dictionary<string, string>
            {
                { "server", "Fastly" },
                { "x-tie", "FRA" },
                { "x-surrogate-key", "abc123 def456" }
            },
            body: "<html>OK</html>");

        using var httpClient = CreateHttpClient(handler);
        var response = await httpClient.GetAsync("https://fastly.example.com");

        var context = new DetectionContext
        {
            Url = "https://fastly.example.com",
            Response = response
        };

        var registry = new ProviderRegistry();
        registry.RegisterProvider(new FastlyProvider());

        var result = await registry.DetectAllAsync(context);

        result.HasCdn.Should().BeTrue();
        result.Cdn!.Name.Should().Be("Fastly");
        result.Evidence.Should().Contain(e => e.Signature == "fastly-tie-header");
    }

    [Fact]
    public async Task DetectAsync_Azure_ReturnsAzureDetection()
    {
        var handler = CreateMockHandler(
            statusCode: 200,
            headers: new Dictionary<string, string>
            {
                { "x-azure-ref", "20240101T000000Z-abc123" },
                { "x-arr-log-id", "abc123-def456" },
                { "server", "Microsoft-IIS/10.0" }
            },
            body: "<html>OK</html>");

        using var httpClient = CreateHttpClient(handler);
        var response = await httpClient.GetAsync("https://azure.example.com");

        var context = new DetectionContext
        {
            Url = "https://azure.example.com",
            Response = response
        };

        var registry = new ProviderRegistry();
        registry.RegisterProvider(new AzureProvider());

        var result = await registry.DetectAllAsync(context);

        result.HasWaf.Should().BeTrue();
        result.Waf!.Name.Should().Be("Azure");
        result.Evidence.Should().Contain(e => e.Signature == "x-azure-ref-header");
    }

    [Fact]
    public async Task DetectAsync_Imperva_ReturnsImpervaDetection()
    {
        var handler = CreateMockHandler(
            statusCode: 200,
            headers: new Dictionary<string, string>
            {
                { "x-cdn", "INCAP" },
                { "set-cookie", "__akavpau_abc=123; visid_incap=def456" },
                { "server", "incap_s-info" }
            },
            body: "<html>OK</html>");

        using var httpClient = CreateHttpClient(handler);
        var response = await httpClient.GetAsync("https://imperva.example.com");

        var context = new DetectionContext
        {
            Url = "https://imperva.example.com",
            Response = response
        };

        var registry = new ProviderRegistry();
        registry.RegisterProvider(new ImpervaProvider());

        var result = await registry.DetectAllAsync(context);

        result.HasWaf.Should().BeTrue();
        result.Waf!.Name.Should().Be("Imperva");
        result.Evidence.Should().Contain(e => e.Signature == "x-cdn-header");
    }

    [Fact]
    public async Task DetectAsync_Sucuri_ReturnsSucuriDetection()
    {
        var handler = CreateMockHandler(
            statusCode: 200,
            headers: new Dictionary<string, string>
            {
                { "x-sucuri-id", "10001" },
                { "x-sucuri-cache", "HIT" },
                { "x-sucuri-block", "Yes" }
            },
            body: "<html>Sucuri Website Firewall</html>");

        using var httpClient = CreateHttpClient(handler);
        var response = await httpClient.GetAsync("https://sucuri.example.com");

        var context = new DetectionContext
        {
            Url = "https://sucuri.example.com",
            Response = response
        };

        var registry = new ProviderRegistry();
        registry.RegisterProvider(new SucuriProvider());

        var result = await registry.DetectAllAsync(context);

        result.HasWaf.Should().BeTrue();
        result.Waf!.Name.Should().Be("Sucuri");
        result.Evidence.Should().Contain(e => e.Signature == "x-sucuri-id-header");
    }

    [Fact]
    public async Task DetectAsync_F5_ReturnsF5Detection()
    {
        var handler = CreateMockHandler(
            statusCode: 200,
            headers: new Dictionary<string, string>
            {
                { "server", "bigip" },
                { "x-ultimate-cp-id", "abc123" },
                { "set-cookie", "F5_TM_COOKIE=abc123" }
            },
            body: "<html>OK</html>");

        using var httpClient = CreateHttpClient(handler);
        var response = await httpClient.GetAsync("https://f5.example.com");

        var context = new DetectionContext
        {
            Url = "https://f5.example.com",
            Response = response
        };

        var registry = new ProviderRegistry();
        registry.RegisterProvider(new F5Provider());

        var result = await registry.DetectAllAsync(context);

        result.HasWaf.Should().BeTrue();
        result.Waf!.Name.Should().Be("F5");
        result.Evidence.Should().Contain(e => e.Signature == "x-ultimate-cp-id-header");
    }

    [Fact]
    public async Task DetectAsync_NoWafHeaders_ReturnsNoDetection()
    {
        var handler = CreateMockHandler(
            statusCode: 200,
            headers: new Dictionary<string, string>
            {
                { "server", "nginx/1.18.0" },
                { "content-type", "text/html" }
            },
            body: "<html><h1>Welcome</h1></html>");

        using var httpClient = CreateHttpClient(handler);
        var response = await httpClient.GetAsync("https://plain.example.com");

        var context = new DetectionContext
        {
            Url = "https://plain.example.com",
            Response = response
        };

        var registry = new ProviderRegistry();
        registry.RegisterProvider(new CloudFlareProvider());
        registry.RegisterProvider(new AwsProvider());
        registry.RegisterProvider(new AkamaiProvider());

        var result = await registry.DetectAllAsync(context);

        result.Detected.Should().BeFalse();
        result.HasWaf.Should().BeFalse();
        result.HasCdn.Should().BeFalse();
        result.Evidence.Should().BeEmpty();
    }

    [Fact]
    public async Task DetectAsync_MultipleProviders_ReturnsBestMatch()
    {
        var handler = CreateMockHandler(
            statusCode: 200,
            headers: new Dictionary<string, string>
            {
                { "cf-ray", "abc123-CDG" },
                { "cf-cache-status", "HIT" }
            },
            body: "<html>OK</html>");

        using var httpClient = CreateHttpClient(handler);
        var response = await httpClient.GetAsync("https://multi.example.com");

        var context = new DetectionContext
        {
            Url = "https://multi.example.com",
            Response = response
        };

        var registry = new ProviderRegistry();
        registry.RegisterProvider(new CloudFlareProvider());
        registry.RegisterProvider(new AwsProvider());
        registry.RegisterProvider(new AkamaiProvider());
        registry.RegisterProvider(new FastlyProvider());

        var result = await registry.DetectAllAsync(context);

        result.HasWaf.Should().BeTrue();
        result.Waf!.Name.Should().Be("CloudFlare");
        result.ProviderScores.Should().ContainKey("CloudFlare");
        result.ProviderScores["CloudFlare"].Should().BeGreaterOrEqualTo(0.60);
    }

    [Fact]
    public async Task DetectAsync_403WithCloudFlare_ReturnsCloudFlare()
    {
        var handler = CreateMockHandler(
            statusCode: 403,
            headers: new Dictionary<string, string>
            {
                { "cf-ray", "abc123-CDG" },
                { "server", "cloudflare" }
            },
            body: "<html><head><title>Access Denied</title></head><body>CloudFlare</body></html>");

        using var httpClient = CreateHttpClient(handler);
        var response = await httpClient.GetAsync("https://blocked.example.com");

        var context = new DetectionContext
        {
            Url = "https://blocked.example.com",
            Response = response
        };

        var registry = new ProviderRegistry();
        registry.RegisterProvider(new CloudFlareProvider());

        var result = await registry.DetectAllAsync(context);

        result.HasWaf.Should().BeTrue();
        result.Waf!.Name.Should().Be("CloudFlare");
        result.Evidence.Should().Contain(e => e.Method == DetectionMethod.StatusCode);
    }

    [Fact]
    public async Task DetectAsync_CustomProvider_IsDetected()
    {
        var handler = CreateMockHandler(
            statusCode: 200,
            headers: new Dictionary<string, string>
            {
                { "x-custom-waf", "CustomWAF/1.0" }
            },
            body: "<html>OK</html>");

        using var httpClient = CreateHttpClient(handler);
        var response = await httpClient.GetAsync("https://custom.example.com");

        var context = new DetectionContext
        {
            Url = "https://custom.example.com",
            Response = response
        };

        var registry = new ProviderRegistry();
        registry.RegisterProvider(new CustomWafProvider());

        var result = await registry.DetectAllAsync(context);

        result.HasWaf.Should().BeTrue();
        result.Waf!.Name.Should().Be("CustomWAF");
    }

    [Fact]
    public async Task DetectAsync_ProviderDisabled_IsNotDetected()
    {
        var handler = CreateMockHandler(
            statusCode: 200,
            headers: new Dictionary<string, string>
            {
                { "cf-ray", "abc123-CDG" }
            },
            body: "<html>OK</html>");

        using var httpClient = CreateHttpClient(handler);
        var response = await httpClient.GetAsync("https://disabled.example.com");

        var context = new DetectionContext
        {
            Url = "https://disabled.example.com",
            Response = response
        };

        var registry = new ProviderRegistry();
        var disabledProvider = new DisabledCloudFlareProvider();

        registry.RegisterProvider(disabledProvider);

        var result = await registry.DetectAllAsync(context);

        result.HasWaf.Should().BeFalse();
    }

    [Fact]
    public void ListProviders_ReturnsAllRegistered()
    {
        var providers = _client.ListProviders();

        providers.Should().HaveCountGreaterOrEqualTo(8);
        providers.Select(p => p.Name).Should().Contain("CloudFlare");
        providers.Select(p => p.Name).Should().Contain("AWS");
        providers.Select(p => p.Name).Should().Contain("Akamai");
    }

    [Fact]
    public void RegisterProvider_ThrowsOnDuplicate()
    {
        Action act = () => _client.RegisterProvider(new CloudFlareProvider());

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*already registered*");
    }

    [Fact]
    public void GetProviderCount_ReturnsCorrectCount()
    {
        _client.GetProviderCount().Should().BeGreaterOrEqualTo(8);
    }

    [Fact]
    public void ToSummary_WithDetection_IncludesAllDetails()
    {
        var result = new DetectionResult
        {
            Url = "https://example.com",
            Waf = new ProviderDetection { Name = "CloudFlare", Confidence = 0.95 },
            Cdn = new ProviderDetection { Name = "CloudFlare", Confidence = 0.95 },
            Evidence = new List<Evidence>
            {
                new() { Method = DetectionMethod.Header, Name = "cf-ray", Value = "abc123-CDG" },
                new() { Method = DetectionMethod.Header, Name = "cf-cache-status", Value = "HIT" }
            },
            DetectionTimeMs = 150,
            Caveats = new List<string> { "Test caveat" }
        };

        var summary = result.ToSummary();

        summary.Should().Contain("https://example.com");
        summary.Should().Contain("CloudFlare");
        summary.Should().Contain("150ms");
        summary.Should().Contain("Test caveat");
    }

    [Fact]
    public void EvidenceScorer_CalculateConfidence_AllMethods()
    {
        var scorer = new EvidenceScorer();

        var headerEvidence = new List<Evidence>
        {
            new() { Method = DetectionMethod.Header, Confidence = 0.9, Weight = 1.0 }
        };

        var dnsEvidence = new List<Evidence>
        {
            new() { Method = DetectionMethod.DNS, Confidence = 0.85, Weight = 1.0 }
        };

        var cookieEvidence = new List<Evidence>
        {
            new() { Method = DetectionMethod.Cookie, Confidence = 0.8, Weight = 1.0 }
        };

        var headerScore = scorer.CalculateConfidence(headerEvidence);
        var dnsScore = scorer.CalculateConfidence(dnsEvidence);
        var cookieScore = scorer.CalculateConfidence(cookieEvidence);

        headerScore.Should().BeGreaterOrEqualTo(dnsScore);
        dnsScore.Should().BeGreaterOrEqualTo(cookieScore);
    }

    [Fact]
    public void EvidenceScorer_MultipleEvidence_AppliesBonus()
    {
        var scorer = new EvidenceScorer();

        var singleEvidence = new List<Evidence>
        {
            new() { Method = DetectionMethod.Header, Confidence = 0.8, Weight = 1.0 }
        };

        var multipleEvidence = new List<Evidence>
        {
            new() { Method = DetectionMethod.Header, Confidence = 0.8, Weight = 1.0 },
            new() { Method = DetectionMethod.Cookie, Confidence = 0.8, Weight = 1.0 },
            new() { Method = DetectionMethod.Header, Confidence = 0.8, Weight = 1.0 }
        };

        var singleScore = scorer.CalculateConfidence(singleEvidence);
        var multipleScore = scorer.CalculateConfidence(multipleEvidence);

        multipleScore.Should().BeGreaterThan(singleScore);
    }

    [Fact]
    public void EvidenceScorer_GenerateCaveats_BodyOnly_ReturnsWarning()
    {
        var scorer = new EvidenceScorer();

        var bodyEvidence = new List<Evidence>
        {
            new() { Method = DetectionMethod.Body, Confidence = 0.5 }
        };

        var caveats = scorer.GenerateCaveats(bodyEvidence, "TestProvider");

        caveats.Should().ContainSingle();
        caveats[0].Should().Contain("body patterns");
    }

    [Fact]
    public void EvidenceScorer_GenerateCaveats_HeaderAndBody_NoWarning()
    {
        var scorer = new EvidenceScorer();

        var evidence = new List<Evidence>
        {
            new() { Method = DetectionMethod.Header, Confidence = 0.8 },
            new() { Method = DetectionMethod.Body, Confidence = 0.5 }
        };

        var caveats = scorer.GenerateCaveats(evidence, "TestProvider");

        caveats.Should().BeEmpty();
    }

    [Fact]
    public void ProviderMetadata_DefaultValues()
    {
        var metadata = new ProviderMetadata();

        metadata.Name.Should().BeEmpty();
        metadata.Version.Should().BeEmpty();
        metadata.ProviderType.Should().Be(0);
        metadata.Enabled.Should().BeFalse();
        metadata.Priority.Should().Be(0);
    }

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
    public void ProviderType_FlagsWork()
    {
        var both = ProviderType.Both;

        both.HasFlag(ProviderType.WAF).Should().BeTrue();
        both.HasFlag(ProviderType.CDN).Should().BeTrue();
        (both | ProviderType.WAF).Should().Be(ProviderType.Both);
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

    private HttpMessageHandler CreateMockHandler(
        int statusCode,
        Dictionary<string, string> headers,
        string body)
    {
        var handler = new MockHttpMessageHandler(statusCode, headers, body);
        return handler;
    }

    private WafHttpClient CreateHttpClient(HttpMessageHandler handler)
    {
        return new WafHttpClient(handler, null, TimeSpan.FromSeconds(5));
    }

    public void Dispose()
    {
        _client?.Dispose();
    }

    private class MockHttpMessageHandler : HttpMessageHandler
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

    private class CustomWafProvider : IDetectionProvider
    {
        public string Name => "CustomWAF";
        public string Version => "1.0.0";
        public string Description => "Custom WAF for integration tests";
        public ProviderType ProviderType => ProviderType.WAF;
        public double ConfidenceBase => 0.9;
        public int Priority => 50;
        public bool Enabled => true;

        public Task<List<Evidence>> DetectAsync(DetectionContext context)
        {
            var evidence = new List<Evidence>();

            if (context.Response is not null &&
                context.Response.Headers.TryGetValue("x-custom-waf", out var value))
            {
                evidence.Add(new Evidence
                {
                    Method = DetectionMethod.Header,
                    Name = "x-custom-waf",
                    Value = value,
                    Confidence = 0.95,
                    Description = "Custom WAF header detected"
                });
            }

            return Task.FromResult(evidence);
        }

        public Task<List<Evidence>> PassiveDetectAsync(HttpResponseData response)
        {
            return DetectAsync(new DetectionContext { Response = response });
        }
    }

    private class DisabledCloudFlareProvider : IDetectionProvider
    {
        public string Name => "CloudFlare";
        public string Version => "2.0.0";
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
