using WafSight.Models;

namespace WafSight.Providers;

/// <summary>
/// Interface for detection providers
/// </summary>
public interface IDetectionProvider
{
    /// <summary>
    /// Provider name
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Provider version
    /// </summary>
    string Version { get; }

    /// <summary>
    /// Provider description
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Provider type (WAF, CDN, or both)
    /// </summary>
    Models.ProviderType ProviderType { get; }

    /// <summary>
    /// Base confidence for this provider
    /// </summary>
    double ConfidenceBase { get; }

    /// <summary>
    /// Detection priority (higher = detected first)
    /// </summary>
    int Priority { get; }

    /// <summary>
    /// Whether the provider is enabled
    /// </summary>
    bool Enabled { get; }

    /// <summary>
    /// Detects provider evidence from context
    /// </summary>
    Task<List<Models.Evidence>> DetectAsync(Models.DetectionContext context);

    /// <summary>
    /// Passive detection (only headers/body, no additional requests)
    /// </summary>
    Task<List<Models.Evidence>> PassiveDetectAsync(HttpResponseData response);
}

/// <summary>
/// Provider metadata
/// </summary>
public class ProviderMetadata
{
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Models.ProviderType ProviderType { get; set; }
    public bool Enabled { get; set; }
    public int Priority { get; set; }
}
