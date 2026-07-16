using FluentAssertions;
using WafSight.Analysis;
using WafSight.Models;
using WafSight.Providers;
using Xunit;

namespace WafSight.Tests.Unit;

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
    public void CalculateConfidence_NullEvidence_ThrowsArgumentNullException()
    {
        var result = () => _scorer.CalculateConfidence(null!);
        result.Should().Throw<ArgumentNullException>();
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
    public void CalculateConfidence_LowConfidenceBodyEvidence_ReturnsLowScore()
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
    public void GetMethodWeight_ValidMethod_ReturnsCorrectWeight()
    {
        EvidenceScorer.GetMethodWeight(DetectionMethod.Header).Should().Be(1.0);
        EvidenceScorer.GetMethodWeight(DetectionMethod.DNS).Should().Be(0.95);
        EvidenceScorer.GetMethodWeight(DetectionMethod.Payload).Should().Be(0.40);
    }

    [Fact]
    public void GetMethodWeight_UnknownMethod_ReturnsDefault()
    {
        var result = EvidenceScorer.GetMethodWeight((DetectionMethod)999);
        result.Should().Be(0.5);
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
}
