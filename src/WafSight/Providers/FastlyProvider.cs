using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using WafSight.Models;

namespace WafSight.Providers;

/// <summary>
/// Fastly CDN detection provider
/// </summary>
public class FastlyProvider : IDetectionProvider
{
    public string Name => "Fastly";
    public string Version => "2.0.0";
    public string Description => "Fastly CDN detection provider";
    public ProviderType ProviderType => ProviderType.CDN;
    public double ConfidenceBase => 0.90;
    public int Priority => 85;
    public bool Enabled => true;

    private readonly ILogger<FastlyProvider>? _logger;

    public FastlyProvider(ILogger<FastlyProvider>? logger = null)
    {
        _logger = logger;
    }

    private static readonly Regex FastlyServerPattern = new(@"(?i)fastly", RegexOptions.Compiled);
    private static readonly Regex FastlyTiePattern = new(@"(?i)x-tie", RegexOptions.Compiled);
    private static readonly Regex FastlySurrogateKeyPattern = new(@"(?i)x-surrogate-key", RegexOptions.Compiled);

    public async Task<List<Evidence>> DetectAsync(DetectionContext context)
    {
        _logger?.LogDebug("Starting Fastly detection for {Url}", context.Url);
        var evidence = new List<Evidence>();

        if (context.Response is not null)
        {
            evidence.AddRange(await CheckHeaders(context.Response));
            _logger?.LogInformation("Fastly evidence found: {Evidence}", evidence.Count > 0 ? evidence.LastOrDefault()?.Name ?? "unknown" : "none");
            _logger?.LogDebug("Fastly detection completed: {Count} evidence(s)", evidence.Count);
        }
        else
        {
            _logger?.LogWarning("No response data for Fastly detection on {Url}", context.Url);
        }

        return evidence;
    }

    public Task<List<Evidence>> PassiveDetectAsync(HttpResponseData response)
    {
        return CheckHeaders(response);
    }

    private Task<List<Evidence>> CheckHeaders(HttpResponseData response)
    {
        var evidence = new List<Evidence>();

        if (response.Headers.TryGetValue("server", out var server) &&
            FastlyServerPattern.IsMatch(server))
        {
            evidence.Add(new Evidence
            {
                Method = DetectionMethod.Header,
                Name = "server",
                Value = server,
                Confidence = 0.85,
                Description = "Fastly server header detected",
                Signature = "fastly-server-header",
                Weight = 0.85
            });
        }

        if (response.Headers.TryGetValue("x-tie", out var tie))
        {
            evidence.Add(new Evidence
            {
                Method = DetectionMethod.Header,
                Name = "x-tie",
                Value = tie,
                Confidence = 0.90,
                Description = "Fastly tie header detected",
                Signature = "fastly-tie-header",
                Weight = 0.90
            });
        }

        if (response.Headers.TryGetValue("x-surrogate-key", out var surrogateKey))
        {
            evidence.Add(new Evidence
            {
                Method = DetectionMethod.Header,
                Name = "x-surrogate-key",
                Value = surrogateKey,
                Confidence = 0.80,
                Description = "Fastly surrogate key header detected",
                Signature = "fastly-surrogate-key-header",
                Weight = 0.80
            });
        }

        if (response.Headers.TryGetValue("via", out var via))
        {
            if (via.Contains("fastly", StringComparison.OrdinalIgnoreCase))
            {
                evidence.Add(new Evidence
                {
                    Method = DetectionMethod.Header,
                    Name = "via",
                    Value = via,
                    Confidence = 0.80,
                    Description = "Fastly via header detected",
                    Signature = "fastly-via-header",
                    Weight = 0.80
                });
            }
        }

        return Task.FromResult(evidence);
    }
}
