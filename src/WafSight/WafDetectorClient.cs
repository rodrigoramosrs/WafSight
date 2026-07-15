using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using WafSight.Analysis;
using WafSight.Http;
using WafSight.Models;
using WafSight.Providers;
using WafSight.Registry;
using WafSight;

namespace WafSight;

/// <summary>
/// Main client for WAF/CDN detection
/// </summary>
public class WafDetectorClient : IWafDetector, IDisposable
{
    private readonly ProviderRegistry _registry;
    private readonly WafHttpClient _httpClient;
    private readonly DnsAnalyzer _dnsAnalyzer;
    private readonly GenericDetector _genericDetector;
    private readonly ILogger<WafDetectorClient>? _logger;

    public WafDetectorClient(
        ILogger<WafDetectorClient>? logger = null,
        TimeSpan? timeout = null)
    {
        _logger = logger;
        _registry = new ProviderRegistry();
        _httpClient = new WafHttpClient(logger: null, timeout: timeout);
        _dnsAnalyzer = new DnsAnalyzer();
        _genericDetector = new GenericDetector();

        RegisterDefaultProviders();
    }

    private void RegisterDefaultProviders()
    {
        _registry.RegisterProvider(new CloudFlareProvider());
        _registry.RegisterProvider(new AwsProvider());
        _registry.RegisterProvider(new AkamaiProvider());
        _registry.RegisterProvider(new FastlyProvider());
        _registry.RegisterProvider(new AzureProvider());
        _registry.RegisterProvider(new ImpervaProvider());
        _registry.RegisterProvider(new SucuriProvider());
        _registry.RegisterProvider(new F5Provider());
    }

    /// <summary>
    /// Registers a custom provider
    /// </summary>
    public void RegisterProvider(IDetectionProvider provider)
    {
        _registry.RegisterProvider(provider);
    }

    /// <inheritdoc/>
    public async Task<DetectionResult> DetectAsync(string url, CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;

        try
        {
            var response = await _httpClient.GetAsync(url, cancellationToken);
            var dnsInfo = await _dnsAnalyzer.ResolveAsync(url, cancellationToken);

            var context = new DetectionContext
            {
                Url = url,
                Response = response,
                DnsInfo = dnsInfo
            };

            var result = await _registry.DetectAllAsync(context);

            if (!result.Detected && response != null)
            {
                var genericResult = await _genericDetector.DetectGenericAsync(
                    context,
                    async u => await _httpClient.GetAsync(u, cancellationToken));

                if (genericResult is not null)
                {
                    result = genericResult;
                }
            }

            result.DetectionTimeMs = (long)(DateTime.UtcNow - startTime).TotalMilliseconds;
            return result;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error detecting WAF/CDN for {Url}", url);

            return new DetectionResult
            {
                Url = url,
                DetectionTimeMs = (long)(DateTime.UtcNow - startTime).TotalMilliseconds,
                Caveats = new List<string> { $"Error during detection: {ex.Message}" }
            };
        }
    }

    /// <inheritdoc/>
    public async Task<Dictionary<string, DetectionResult>> DetectBatchAsync(
        IEnumerable<string> urls,
        int maxConcurrency = 3,
        CancellationToken cancellationToken = default)
    {
        var semaphore = new SemaphoreSlim(maxConcurrency);
        var results = new ConcurrentDictionary<string, DetectionResult>();

        var tasks = urls.Select(async url =>
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                var result = await DetectAsync(url, cancellationToken);
                results[url] = result;
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);
        return results.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    }

    /// <inheritdoc/>
    public IReadOnlyList<ProviderMetadata> ListProviders()
    {
        return _registry.ListProviders();
    }

    /// <inheritdoc/>
    public int GetProviderCount()
    {
        return _registry.GetProviderCount();
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}
