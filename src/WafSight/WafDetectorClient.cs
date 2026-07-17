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
    private readonly ILoggerFactory _loggerFactory;

    public WafDetectorClient(
        ILoggerFactory? loggerFactory = null,
        TimeSpan? timeout = null)
    {
        _loggerFactory = loggerFactory ?? LoggerFactory.Create(builder => { });
        _logger = _loggerFactory.CreateLogger<WafDetectorClient>();
        _registry = new ProviderRegistry(_loggerFactory.CreateLogger<ProviderRegistry>());
        _httpClient = new WafHttpClient(logger: _loggerFactory.CreateLogger<WafHttpClient>(), timeout: timeout);
        _dnsAnalyzer = new DnsAnalyzer(_loggerFactory.CreateLogger<DnsAnalyzer>());
        _genericDetector = new GenericDetector(_loggerFactory.CreateLogger<GenericDetector>());

        RegisterDefaultProviders();
    }

    private void RegisterDefaultProviders()
    {
        _registry.RegisterProvider(new CloudFlareProvider(_loggerFactory.CreateLogger<CloudFlareProvider>()));
        _registry.RegisterProvider(new AwsProvider(_loggerFactory.CreateLogger<AwsProvider>()));
        _registry.RegisterProvider(new AkamaiProvider(_loggerFactory.CreateLogger<AkamaiProvider>()));
        _registry.RegisterProvider(new FastlyProvider(_loggerFactory.CreateLogger<FastlyProvider>()));
        _registry.RegisterProvider(new AzureProvider(_loggerFactory.CreateLogger<AzureProvider>()));
        _registry.RegisterProvider(new ImpervaProvider(_loggerFactory.CreateLogger<ImpervaProvider>()));
        _registry.RegisterProvider(new SucuriProvider(_loggerFactory.CreateLogger<SucuriProvider>()));
        _registry.RegisterProvider(new F5Provider(_loggerFactory.CreateLogger<F5Provider>()));
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
        _logger?.LogDebug("Starting detection for URL: {Url}", url);

        try
        {
            var response = await _httpClient.GetAsync(url, cancellationToken);
            _logger?.LogInformation("HTTP request completed for {Url}, Status: {Status}", url, response?.StatusCode);

            var dnsInfo = await _dnsAnalyzer.ResolveAsync(url, cancellationToken);
            if (dnsInfo is not null)
            {
                _logger?.LogDebug("DNS resolved for {Url}: A={ARecords}, CNAME={CnameCount}", 
                    url, dnsInfo.ARecords?.Count ?? 0, dnsInfo.Cnames?.Count ?? 0);
            }

            var context = new DetectionContext
            {
                Url = url,
                Response = response,
                DnsInfo = dnsInfo
            };

            var result = await _registry.DetectAllAsync(context);
            _logger?.LogInformation("Detection completed for {Url}: WAF={Waf}, CDN={Cdn}, Time={Time}ms",
                url, result.HasWaf ? result.Waf?.Name : "None",
                result.HasCdn ? result.Cdn?.Name : "None",
                (DateTime.UtcNow - startTime).TotalMilliseconds);

            if (!result.Detected && response != null)
            {
                _logger?.LogDebug("Running generic detection for {Url}", url);
                var genericResult = await _genericDetector.DetectGenericAsync(
                    context,
                    async u => await _httpClient.GetAsync(u, cancellationToken));

                if (genericResult is not null)
                {
                    _logger?.LogInformation("Generic detection found for {Url}: {Type}", url, genericResult.Waf?.Name ?? genericResult.Cdn?.Name);
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
    public async Task<DetectionResult> DetectFromResponseAsync(
        HttpResponseData response,
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        var url = response.Url;
        _logger?.LogDebug("Starting passive detection from response for url: {Url}", url);

        try
        {
            var context = new DetectionContext
            {
                Url = url,
                Response = response
            };

            var result = await _registry.DetectAllAsync(context);
            result.DetectionTimeMs = (long)(DateTime.UtcNow - startTime).TotalMilliseconds;

            _logger?.LogInformation("Passive detection completed for {Url}: WAF={Waf}, CDN={Cdn}, Time={Time}ms",
                url, result.HasWaf ? result.Waf?.Name : "None",
                result.HasCdn ? result.Cdn?.Name : "None",
                result.DetectionTimeMs);

            return result;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error during passive detection for {Url}", url);

            return new DetectionResult
            {
                Url = url,
                DetectionTimeMs = (long)(DateTime.UtcNow - startTime).TotalMilliseconds,
                Caveats = new List<string> { $"Passive detection failed: {ex.Message}" }
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
