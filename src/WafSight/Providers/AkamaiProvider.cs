using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using WafSight.Models;

namespace WafSight.Providers;

/// <summary>
/// Akamai WAF/CDN detection provider
/// </summary>
public class AkamaiProvider : IDetectionProvider
{
    public string Name => "Akamai";
    public string Version => "2.0.0";
    public string Description => "Akamai WAF/CDN detection provider";
    public ProviderType ProviderType => ProviderType.Both;
    public double ConfidenceBase => 0.92;
    public int Priority => 90;
    public bool Enabled => true;

    private readonly ILogger<AkamaiProvider>? _logger;

    public AkamaiProvider(ILogger<AkamaiProvider>? logger = null)
    {
        _logger = logger;
    }

    private static readonly Regex AkamaiServerPattern = new(@"(?i)xakamai-disclose-info", RegexOptions.Compiled);
    private static readonly Regex AkamaiViaPattern = new(@"(?i)akamai-g2(?:\.?)?[a-z]?/?(?:\.?)?[0-9.]*", RegexOptions.Compiled);
    private static readonly Regex AkamaiEdgePattern = new(@"(?i)x-akamai-transformed", RegexOptions.Compiled);
    private static readonly Regex AkamaiRequestPattern = new(@"(?i)[a-f0-9]{16}", RegexOptions.Compiled);

    public Task<List<Evidence>> DetectAsync(DetectionContext context)
    {
        _logger?.LogDebug("Starting Akamai detection for {Url}", context.Url);
        var evidence = new List<Evidence>();

        if (context.Response is not null)
        {
            evidence.AddRange(CheckHeaders(context.Response));
            evidence.AddRange(CheckCookies(context.Response));
            _logger?.LogInformation("Akamai evidence found: {Evidence}", evidence.Count > 0 ? evidence.LastOrDefault()?.Name ?? "unknown" : "none");
            _logger?.LogDebug("Akamai detection completed: {Count} evidence(s)", evidence.Count);
        }
        else
        {
            _logger?.LogWarning("No response data for Akamai detection on {Url}", context.Url);
        }

        return Task.FromResult(evidence);
    }

    public Task<List<Evidence>> PassiveDetectAsync(HttpResponseData response)
    {
        var evidence = new List<Evidence>();
        evidence.AddRange(CheckHeaders(response));
        evidence.AddRange(CheckCookies(response));
        return Task.FromResult(evidence);
    }

    private List<Evidence> CheckHeaders(HttpResponseData response)
    {
        var evidence = new List<Evidence>();

        if (response.Headers.TryGetValue("x-akamai-transformed", out var transformed))
        {
            evidence.Add(new Evidence
            {
                Method = DetectionMethod.Header,
                Name = "x-akamai-transformed",
                Value = transformed,
                Confidence = 0.90,
                Description = "Akamai transform header detected",
                Signature = "x-akamai-transformed-header",
                Weight = 0.95
            });
        }

        if (response.Headers.TryGetValue("via", out var via) &&
            AkamaiViaPattern.IsMatch(via))
        {
            evidence.Add(new Evidence
            {
                Method = DetectionMethod.Header,
                Name = "via",
                Value = via,
                Confidence = 0.85,
                Description = "Akamai via header detected",
                Signature = "akamai-via-header",
                Weight = 0.90
            });
        }

        if (response.Headers.TryGetValue("xakamai-disclose-info", out var disclose))
        {
            evidence.Add(new Evidence
            {
                Method = DetectionMethod.Header,
                Name = "xakamai-disclose-info",
                Value = disclose,
                Confidence = 0.80,
                Description = "Akamai disclose info header detected",
                Signature = "xakamai-disclose-info-header",
                Weight = 0.80
            });
        }

        if (response.Headers.TryGetValue("server", out var server))
        {
            if (server.Equals("AkamaiG2", StringComparison.OrdinalIgnoreCase) ||
                server.Equals("AkamaiG221", StringComparison.OrdinalIgnoreCase))
            {
                evidence.Add(new Evidence
                {
                    Method = DetectionMethod.Header,
                    Name = "server",
                    Value = server,
                    Confidence = 0.85,
                    Description = "Akamai server header detected",
                    Signature = "akamai-server-header",
                    Weight = 0.85
                });
            }
        }

        return evidence;
    }

    private List<Evidence> CheckCookies(HttpResponseData response)
    {
        var evidence = new List<Evidence>();

        if (response.Headers.TryGetValue("set-cookie", out var cookies))
        {
            if (cookies.Contains("__akavpau_") || cookies.Contains("__aaid_") || cookies.Contains("__utm"))
            {
                evidence.Add(new Evidence
                {
                    Method = DetectionMethod.Cookie,
                    Name = "set-cookie",
                    Value = cookies,
                    Confidence = 0.80,
                    Description = "Akamai cookie detected",
                    Signature = "akamai-cookie",
                    Weight = 0.80
                });
            }
        }

        return evidence;
    }
}
