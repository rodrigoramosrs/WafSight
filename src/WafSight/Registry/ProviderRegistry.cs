using WafSight.Analysis;
using WafSight.Models;
using WafSight.Providers;

namespace WafSight.Registry;

/// <summary>
/// Registry to manage detection providers
/// </summary>
public class ProviderRegistry
{
    private readonly Dictionary<string, IDetectionProvider> _providers = new();
    private readonly Dictionary<string, ProviderMetadata> _metadata = new();
    private readonly EvidenceScorer _scorer = new();

    /// <summary>
    /// Registers a new provider
    /// </summary>
    public void RegisterProvider(IDetectionProvider provider)
    {
        if (_providers.ContainsKey(provider.Name))
            throw new InvalidOperationException($"Provider '{provider.Name}' is already registered");

        _providers[provider.Name] = provider;
        _metadata[provider.Name] = new ProviderMetadata
        {
            Name = provider.Name,
            Version = provider.Version,
            Description = provider.Description,
            ProviderType = provider.ProviderType,
            Enabled = provider.Enabled,
            Priority = provider.Priority
        };
    }

    /// <summary>
    /// Runs detection with all registered providers
    /// </summary>
    public async Task<DetectionResult> DetectAllAsync(DetectionContext context)
    {
        var startTime = DateTime.UtcNow;
        var providerScores = new Dictionary<string, double>();
        var allEvidence = new Dictionary<string, List<Models.Evidence>>();

        foreach (var providerName in _providers.Keys)
        {
            allEvidence[providerName] = new List<Models.Evidence>();
        }

        var tasks = _providers.Values
            .Where(p => p.Enabled)
            .Select(async provider =>
            {
                try
                {
                    var evidence = await provider.DetectAsync(context);
                    return (provider.Name, evidence, provider.ConfidenceBase);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Provider '{provider.Name}' failed: {ex.Message}");
                    return (provider.Name, new List<Models.Evidence>(), 0.0);
                }
            });

        var results = await Task.WhenAll(tasks);

        foreach (var (name, evidence, _) in results)
        {
            if (evidence.Any())
            {
                allEvidence[name] = evidence;
                var score = _scorer.CalculateConfidence(evidence);
                providerScores[name] = score;
            }
        }

        var bestWaf = FindBestProvider(providerScores, allEvidence, Models.ProviderType.WAF);
        var bestCdn = FindBestProvider(providerScores, allEvidence, Models.ProviderType.CDN);

        var detectionTime = (long)(DateTime.UtcNow - startTime).TotalMilliseconds;

        var flatEvidence = allEvidence.Values.SelectMany(e => e).ToList();

        var caveats = new List<string>();
        foreach (var (provider, evidenceList) in allEvidence)
        {
            if (evidenceList.Any())
            {
                caveats.AddRange(_scorer.GenerateCaveats(evidenceList, provider));
            }
        }

        return new DetectionResult
        {
            Url = context.Url,
            Waf = bestWaf,
            Cdn = bestCdn,
            ProviderScores = providerScores,
            Evidence = flatEvidence,
            DetectionTimeMs = detectionTime,
            Caveats = caveats.Distinct().ToList()
        };
    }

    private ProviderDetection? FindBestProvider(
        Dictionary<string, double> scores,
        Dictionary<string, List<Models.Evidence>> evidenceMap,
        Models.ProviderType targetType)
    {
        ProviderDetection? best = null;
        double bestScore = 0.0;

        foreach (var (name, score) in scores)
        {
            if (!_metadata.TryGetValue(name, out var metadata))
                continue;

            if (!metadata.ProviderType.HasFlag(targetType) && metadata.ProviderType != Models.ProviderType.Both)
                continue;

            var evidence = evidenceMap.GetValueOrDefault(name, new List<Models.Evidence>());
            var hasTier1 = _scorer.HasTier1Evidence(evidence);

            if (score > bestScore && score >= 0.60 && hasTier1)
            {
                bestScore = score;
                best = new ProviderDetection { Name = name, Confidence = score };
            }
        }

        return best;
    }

    /// <summary>
    /// Lists all registered providers
    /// </summary>
    public IReadOnlyList<ProviderMetadata> ListProviders()
    {
        return _metadata.Values.OrderByDescending(m => m.Priority).ToList();
    }

    /// <summary>
    /// Gets the number of registered providers
    /// </summary>
    public int GetProviderCount() => _providers.Count;
}
