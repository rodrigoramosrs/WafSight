using FluentAssertions;
using WafSight.Models;
using Xunit;

namespace WafSight.Tests.Unit;

public class DetectionResultTests
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
    public void ToSummary_NoDetection_ShowsNotDetected()
    {
        var result = new DetectionResult
        {
            Url = "https://example.com"
        };

        var summary = result.ToSummary();

        summary.Should().Contain("Not detected");
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
