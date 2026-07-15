using WafSight.Models;
using WafSight.Providers;

/// <summary>
/// Main WAF/CDN detection interface
/// </summary>
public interface IWafDetector : IDisposable
{
    /// <summary>
    /// Detects WAF/CDN on a URL
    /// </summary>
    Task<DetectionResult> DetectAsync(string url, CancellationToken cancellationToken = default);

    /// <summary>
    /// Detects WAF/CDN on multiple URLs
    /// </summary>
    Task<Dictionary<string, DetectionResult>> DetectBatchAsync(
        IEnumerable<string> urls,
        int maxConcurrency = 3,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all registered providers
    /// </summary>
    IReadOnlyList<ProviderMetadata> ListProviders();

    /// <summary>
    /// Gets the number of registered providers
    /// </summary>
    int GetProviderCount();

    /// <summary>
    /// Registers a custom provider
    /// </summary>
    void RegisterProvider(IDetectionProvider provider);
}
