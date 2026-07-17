using FluentAssertions;
using WafSight.Models;
using WafSight.Providers;
using Xunit;

namespace WafSight.Tests.Integration;

public class PassiveDetectionTests : IDisposable
{
    private readonly WafDetectorClient _client;

    public PassiveDetectionTests()
    {
        _client = new WafDetectorClient();
    }

    [Fact]
    public async Task DetectFromResponseAsync_CloudFlareHeaders_DetectsCloudFlare()
    {
        var response = new HttpResponseData
        {
            Url = "https://example.com/admin",
            StatusCode = 200,
            Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "cf-ray", "abc123-CDG" },
                { "cf-cache-status", "HIT" },
                { "server", "cloudflare" }
            },
            Body = "<html>OK</html>"
        };

        var result = await _client.DetectFromResponseAsync(response);

        result.HasWaf.Should().BeTrue();
        result.Waf!.Name.Should().Be("CloudFlare");
        result.Waf.Confidence.Should().BeGreaterThan(0.6);
        result.HasCdn.Should().BeTrue();
        result.Cdn!.Name.Should().Be("CloudFlare");
        result.Evidence.Should().HaveCountGreaterOrEqualTo(3);
    }

    [Fact]
    public async Task DetectFromResponseAsync_CloudFlareChallengeBody_DetectsCloudFlare()
    {
        var response = new HttpResponseData
        {
            Url = "https://example.com/protected",
            StatusCode = 403,
            Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "server", "cloudflare" },
                { "cf-ray", "xyz789-CDG" }
            },
            Body = "<html><title>Checking your browser before accessing cloudflare</title><form id=\"cf_chl_jschl_tk\"></form></html>"
        };

        var result = await _client.DetectFromResponseAsync(response);

        result.HasWaf.Should().BeTrue();
        result.Waf!.Name.Should().Be("CloudFlare");
        result.Evidence.Should().Contain(e => e.Signature == "cf-challenge-body");
    }

    [Fact]
    public async Task DetectFromResponseAsync_CloudFlare200WithChallenge_DetectsCloudFlare()
    {
        var response = new HttpResponseData
        {
            Url = "https://example.com/restricted",
            StatusCode = 200,
            Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "server", "cloudflare" },
                { "cf-ray", "abc456-CDG" }
            },
            Body = "<html><title>Checking your browser</title><script>cf_chl_jschl_tk</script></html>"
        };

        var result = await _client.DetectFromResponseAsync(response);

        result.HasWaf.Should().BeTrue();
        result.Waf!.Name.Should().Be("CloudFlare");
        result.Evidence.Should().Contain(e => e.Method == DetectionMethod.Body);
    }

    [Fact]
    public async Task DetectFromResponseAsync_ImpervaCookies_DetectsImperva()
    {
        var response = new HttpResponseData
        {
            Url = "https://example.com/api/data",
            StatusCode = 200,
            Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "server", "incapsula" },
                { "set-cookie", "incap_ses=abc123; visid_incap=def456" },
                { "x-cdn", "INCAP" }
            },
            Body = "<html>OK</html>"
        };

        var result = await _client.DetectFromResponseAsync(response);

        result.HasWaf.Should().BeTrue();
        result.Waf!.Name.Should().Be("Imperva");
    }

    [Fact]
    public async Task DetectFromResponseAsync_NoWafHeaders_ReturnsNoDetection()
    {
        var response = new HttpResponseData
        {
            Url = "https://example.com/",
            StatusCode = 200,
            Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "content-type", "text/html" },
                { "server", "nginx/1.24.0" }
            },
            Body = "<html><body>Welcome</body></html>"
        };

        var result = await _client.DetectFromResponseAsync(response);

        result.HasWaf.Should().BeFalse();
        result.HasCdn.Should().BeFalse();
        result.Detected.Should().BeFalse();
    }

    [Fact]
    public async Task DetectFromResponseAsync_AkamaiHeaders_DetectsAkamai()
    {
        var response = new HttpResponseData
        {
            Url = "https://example.com/",
            StatusCode = 200,
            Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "server", "AkamaiG2" },
                { "x-akamai-transformed", "yes" },
                { "via", "Akamai-G2/1.0" }
            },
            Body = "<html>OK</html>"
        };

        var result = await _client.DetectFromResponseAsync(response);

        result.HasWaf.Should().BeTrue();
        result.Waf!.Name.Should().Be("Akamai");
    }

    [Fact]
    public async Task DetectFromResponseAsync_F5Headers_DetectsF5()
    {
        var response = new HttpResponseData
        {
            Url = "https://example.com/",
            StatusCode = 200,
            Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "server", "BigIP" },
                { "x-request-id", "abc123" }
            },
            Body = "<html>OK</html>"
        };

        var result = await _client.DetectFromResponseAsync(response);

        result.HasWaf.Should().BeTrue();
        result.Waf!.Name.Should().Be("F5");
    }

    [Fact]
    public async Task DetectFromResponseAsync_CustomProvider_IntegratesWithRegistry()
    {
        var provider = new CustomWafProvider();
        _client.RegisterProvider(provider);

        var response = new HttpResponseData
        {
            Url = "https://example.com/",
            StatusCode = 200,
            Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "x-custom-waf", "blocked" }
            },
            Body = "<html>OK</html>"
        };

        var result = await _client.DetectFromResponseAsync(response);

        result.HasWaf.Should().BeTrue();
        result.Waf!.Name.Should().Be("CustomWAF");
    }

    [Fact]
    public async Task DetectFromResponseAsync_StatusCode403WithCloudFlare_DetectsCloudFlare()
    {
        var response = new HttpResponseData
        {
            Url = "https://example.com/blocked-path",
            StatusCode = 403,
            Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "cf-ray", "xyz789-CDG" },
                { "server", "cloudflare" }
            },
            Body = "<html>Access Denied</html>"
        };

        var result = await _client.DetectFromResponseAsync(response);

        result.HasWaf.Should().BeTrue();
        result.Waf!.Name.Should().Be("CloudFlare");
    }

    [Fact]
    public async Task DetectFromResponseAsync_StatusCode429WithCloudFlare_DetectsCloudFlare()
    {
        var response = new HttpResponseData
        {
            Url = "https://example.com/api/",
            StatusCode = 429,
            Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "server", "cloudflare" },
                { "cf-ray", "xyz789-CDG" },
                { "retry-after", "120" }
            },
            Body = "<html>Too Many Requests</html>"
        };

        var result = await _client.DetectFromResponseAsync(response);

        result.HasWaf.Should().BeTrue();
        result.Waf!.Name.Should().Be("CloudFlare");
    }

    public void Dispose()
    {
        _client?.Dispose();
    }

    private class CustomWafProvider : IDetectionProvider
    {
        public string Name => "CustomWAF";
        public string Version => "1.0.0";
        public string Description => "Custom WAF for passive detection tests";
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
}
