namespace WafSight.Models;

/// <summary>
/// WAF/CDN detection result
/// </summary>
public class DetectionResult
{
    /// <summary>
    /// Analyzed URL
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Detected WAF (if any)
    /// </summary>
    public ProviderDetection? Waf { get; set; }

    /// <summary>
    /// Detected CDN (if any)
    /// </summary>
    public ProviderDetection? Cdn { get; set; }

    /// <summary>
    /// All collected evidence
    /// </summary>
    public List<Evidence> Evidence { get; set; } = new();

    /// <summary>
    /// Scores per provider
    /// </summary>
    public Dictionary<string, double> ProviderScores { get; set; } = new();

    /// <summary>
    /// Detection time in milliseconds
    /// </summary>
    public long DetectionTimeMs { get; set; }

    /// <summary>
    /// Detection timestamp
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Warnings and caveats about the detection
    /// </summary>
    public List<string> Caveats { get; set; } = new();

    /// <summary>
    /// Whether a WAF was detected
    /// </summary>
    public bool HasWaf => Waf != null;

    /// <summary>
    /// Whether a CDN was detected
    /// </summary>
    public bool HasCdn => Cdn != null;

    /// <summary>
    /// Whether anything was detected (WAF or CDN)
    /// </summary>
    public bool Detected => HasWaf || HasCdn;

    /// <summary>
    /// Summary text representation
    /// </summary>
    public string ToSummary()
    {
        var lines = new List<string>
        {
            $"URL: {Url}",
            $"WAF: {Waf?.Name ?? "Not detected"} ({Waf?.Confidence.ToString("P0") ?? "N/A"})"
        };

        if (Cdn is not null)
            lines.Add($"CDN: {Cdn.Name} ({Cdn.Confidence:P0})");

        lines.Add($"Evidence: {Evidence.Count} | Time: {DetectionTimeMs}ms");

        if (Caveats.Count > 0)
        {
            lines.Add("Caveats:");
            foreach (var caveat in Caveats)
                lines.Add($"  - {caveat}");
        }

        return string.Join(Environment.NewLine, lines);
    }
}

/// <summary>
/// Detection of a specific provider
/// </summary>
public class ProviderDetection
{
    /// <summary>
    /// Provider name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Confidence level (0.0 to 1.0)
    /// </summary>
    public double Confidence { get; set; }

    /// <summary>
    /// Detected version (if available)
    /// </summary>
    public string? Version { get; set; }
}
