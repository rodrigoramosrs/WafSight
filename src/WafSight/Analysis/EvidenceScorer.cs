using Microsoft.Extensions.Logging;
using WafSight.Models;

namespace WafSight.Analysis;

/// <summary>
/// Calculates confidence scores based on weighted evidence
/// </summary>
public class EvidenceScorer
{
    private static readonly Dictionary<Models.DetectionMethod, double> MethodWeights = new()
    {
        { Models.DetectionMethod.Header, 1.0 },
        { Models.DetectionMethod.DNS, 0.95 },
        { Models.DetectionMethod.Certificate, 0.90 },
        { Models.DetectionMethod.Cookie, 0.85 },
        { Models.DetectionMethod.StatusCode, 0.75 },
        { Models.DetectionMethod.Timing, 0.70 },
        { Models.DetectionMethod.Body, 0.50 },
        { Models.DetectionMethod.Payload, 0.40 }
    };

    private readonly ILogger<EvidenceScorer>? _logger;

    public EvidenceScorer(ILoggerFactory? loggerFactory = null)
    {
        _logger = loggerFactory?.CreateLogger<EvidenceScorer>();
    }

    /// <summary>
    /// Calculates confidence score for a set of evidence
    /// </summary>
    public double CalculateConfidence(IEnumerable<Models.Evidence> evidence)
    {
        var evidenceList = evidence.ToList();
        _logger?.LogDebug("Starting confidence scoring for {Count} evidence item(s)", evidenceList.Count);

        if (!evidenceList.Any())
            return 0.0;

        var totalScore = 0.0;
        var totalWeight = 0.0;

        foreach (var ev in evidenceList)
        {
            var methodWeight = GetMethodWeight(ev.Method);
            var evidenceWeight = ev.Weight * methodWeight;

            totalScore += ev.Confidence * evidenceWeight;
            totalWeight += evidenceWeight;

            _logger?.LogDebug("Scoring evidence '{Name}' (method={Method}, confidence={Confidence}, weight={Weight}): weightedScore={Score}",
                ev.Name, ev.Method, ev.Confidence, evidenceWeight, ev.Confidence * evidenceWeight);
        }

        if (totalWeight == 0)
            return 0.0;

        var score = totalScore / totalWeight;

        var evidenceCount = evidenceList.Count;
        if (evidenceCount >= 3)
            score = Math.Min(score * 1.1, 1.0);
        else if (evidenceCount >= 2)
            score = Math.Min(score * 1.05, 1.0);

        var finalScore = Math.Clamp(score, 0.0, 1.0);
        _logger?.LogInformation("Confidence score calculated: {Score:F3}", finalScore);

        return finalScore;
    }

    /// <summary>
    /// Gets the weight of a detection method
    /// </summary>
    public static double GetMethodWeight(Models.DetectionMethod method)
    {
        return MethodWeights.TryGetValue(method, out var weight) ? weight : 0.5;
    }

    /// <summary>
    /// Checks if there is Tier 1 evidence (high confidence)
    /// </summary>
    public bool HasTier1Evidence(IEnumerable<Models.Evidence> evidence)
    {
        var tier1Methods = new[]
        {
            Models.DetectionMethod.Header,
            Models.DetectionMethod.DNS,
            Models.DetectionMethod.Certificate,
            Models.DetectionMethod.Cookie
        };

        var tier1Evidence = evidence.Where(e => tier1Methods.Contains(e.Method)).ToList();
        if (tier1Evidence.Any())
        {
            _logger?.LogInformation("Tier 1 evidence detected: {Count} item(s) - {Names}",
                tier1Evidence.Count,
                string.Join(", ", tier1Evidence.Select(e => e.Name)));
        }

        return tier1Evidence.Any();
    }

    /// <summary>
    /// Generates caveats based on evidence
    /// </summary>
    public List<string> GenerateCaveats(IEnumerable<Models.Evidence> evidence, string providerName)
    {
        var caveats = new List<string>();
        var evidenceList = evidence.ToList();

        var hasHeaderEvidence = evidenceList.Any(e =>
            e.Method == Models.DetectionMethod.Header ||
            e.Method == Models.DetectionMethod.Cookie);
        var hasBodyEvidence = evidenceList.Any(e => e.Method == Models.DetectionMethod.Body);

        if (hasBodyEvidence && !hasHeaderEvidence)
        {
            caveats.Add($"Detection of {providerName} based only on body patterns - consider additional verification");
        }

        var timingCount = evidenceList.Count(e => e.Method == Models.DetectionMethod.Timing);
        if (evidenceList.Count > 0 && (double)timingCount / evidenceList.Count > 0.5)
        {
            caveats.Add($"Detection of {providerName} relies heavily on timing analysis - results may vary with network conditions");
        }

        return caveats;
    }
}
