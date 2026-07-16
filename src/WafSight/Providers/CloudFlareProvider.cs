using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using WafSight.Models;

namespace WafSight.Providers;

/// <summary>
/// CloudFlare WAF/CDN detection provider
/// </summary>
public class CloudFlareProvider : IDetectionProvider
{
    public string Name => "CloudFlare";
    public string Version => "2.0.0";
    public string Description => "CloudFlare WAF/CDN detection provider";
    public ProviderType ProviderType => ProviderType.Both;
    public double ConfidenceBase => 0.95;
    public int Priority => 100;
    public bool Enabled => true;

    private readonly ILogger<CloudFlareProvider>? _logger;

    public CloudFlareProvider(ILogger<CloudFlareProvider>? logger = null)
    {
        _logger = logger;
    }

    private static readonly Regex CfRayPattern = new(@"^[a-f0-9]+-[A-Z]{3}$", RegexOptions.Compiled);
    private static readonly Regex CfCachePattern = new(@"(?i)(HIT|MISS|EXPIRED|BYPASS|DYNAMIC|REVALIDATED)", RegexOptions.Compiled);
    private static readonly Regex CfServerPattern = new(@"(?i)cloudflare", RegexOptions.Compiled);
    private static readonly Regex CfChallengePattern = new(@"(?i)(checking your browser.*cloudflare|cf_chl_jschl_tk|cf_chl_captcha_tk)", RegexOptions.Compiled);

    public Task<List<Evidence>> DetectAsync(DetectionContext context)
    {
        _logger?.LogDebug("Starting CloudFlare detection for {Url}", context.Url);
        var evidence = new List<Evidence>();

        if (context.Response is not null)
        {
            evidence.AddRange(CheckHeaders(context.Response));
            evidence.AddRange(CheckBody(context.Response));
            evidence.AddRange(CheckCookies(context.Response));
            evidence.AddRange(CheckStatusCodes(context.Response));
            
            _logger?.LogInformation("CloudFlare detection completed for {Url}: {Count} evidence(s) found",
                context.Url, evidence.Count);
        }
        else
        {
            _logger?.LogWarning("No response data for CloudFlare detection on {Url}", context.Url);
        }

        return Task.FromResult(evidence);
    }

    public Task<List<Evidence>> PassiveDetectAsync(HttpResponseData response)
    {
        var evidence = new List<Evidence>();
        evidence.AddRange(CheckHeaders(response));
        evidence.AddRange(CheckBody(response));
        evidence.AddRange(CheckCookies(response));
        evidence.AddRange(CheckStatusCodes(response));
        return Task.FromResult(evidence);
    }

    private List<Evidence> CheckHeaders(HttpResponseData response)
    {
        var evidence = new List<Evidence>();

        if (response.Headers.TryGetValue("cf-ray", out var cfRay) && CfRayPattern.IsMatch(cfRay))
        {
            _logger?.LogDebug("CloudFlare cf-ray header detected: {Value}", cfRay);
            evidence.Add(new Evidence
            {
                Method = DetectionMethod.Header,
                Name = "cf-ray",
                Value = cfRay,
                Confidence = 0.95,
                Description = "CloudFlare Ray ID header detected",
                Signature = "cf-ray-header",
                Weight = 1.0
            });
        }

        if (response.Headers.TryGetValue("cf-cache-status", out var cacheStatus) &&
            CfCachePattern.IsMatch(cacheStatus))
        {
            _logger?.LogInformation("CloudFlare cf-cache-status detected: {Value}", cacheStatus);
            evidence.Add(new Evidence
            {
                Method = DetectionMethod.Header,
                Name = "cf-cache-status",
                Value = cacheStatus,
                Confidence = 0.90,
                Description = "CloudFlare cache status header detected",
                Signature = "cf-cache-status-header",
                Weight = 0.95
            });
        }

        if (response.Headers.TryGetValue("server", out var server) &&
            CfServerPattern.IsMatch(server))
        {
            _logger?.LogInformation("CloudFlare server header detected: {Value}", server);
            evidence.Add(new Evidence
            {
                Method = DetectionMethod.Header,
                Name = "server",
                Value = server,
                Confidence = 0.85,
                Description = "CloudFlare server header detected",
                Signature = "cloudflare-server-header",
                Weight = 0.90
            });
        }

        var cfHeaders = new (string Header, string Description, double Confidence, string Signature)[]
        {
            ("cf-connecting-ip", "CloudFlare connecting IP header", 0.80, "cf-connecting-ip-header"),
            ("cf-ipcountry", "CloudFlare IP country header", 0.75, "cf-ipcountry-header"),
            ("cf-visitor", "CloudFlare visitor header", 0.75, "cf-visitor-header"),
            ("cf-request-id", "CloudFlare request ID header", 0.85, "cf-request-id-header")
        };

        foreach (var (headerName, description, confidence, signature) in cfHeaders)
        {
            if (response.Headers.TryGetValue(headerName, out var value))
            {
                _logger?.LogDebug("CloudFlare header detected: {Header}={Value}", headerName, value);
                evidence.Add(new Evidence
                {
                    Method = DetectionMethod.Header,
                    Name = headerName,
                    Value = value,
                    Confidence = confidence,
                    Description = description,
                    Signature = signature,
                    Weight = 0.85
                });
            }
        }

        return evidence;
    }

    private List<Evidence> CheckBody(HttpResponseData response)
    {
        var evidence = new List<Evidence>();
        var body = response.Body?.ToLower() ?? "";

        if (CfChallengePattern.IsMatch(body))
        {
            _logger?.LogInformation("CloudFlare challenge page detected in response body");
            evidence.Add(new Evidence
            {
                Method = DetectionMethod.Body,
                Name = "challenge-page",
                Value = "detected",
                Confidence = 0.70,
                Description = "CloudFlare browser challenge page detected",
                Signature = "cf-challenge-body",
                Weight = 0.50
            });
        }

        return evidence;
    }

    private List<Evidence> CheckCookies(HttpResponseData response)
    {
        var evidence = new List<Evidence>();

        if (response.Headers.TryGetValue("set-cookie", out var cookies))
        {
            if (cookies.Contains("__cfduid") || cookies.Contains("__cf_bm"))
            {
                _logger?.LogInformation("CloudFlare cookie detected: {Cookie}", cookies);
                evidence.Add(new Evidence
                {
                    Method = DetectionMethod.Cookie,
                    Name = "set-cookie",
                    Value = cookies,
                    Confidence = 0.85,
                    Description = "CloudFlare cookie detected",
                    Signature = "cloudflare-cookie",
                    Weight = 0.85
                });
            }
        }

        return evidence;
    }

    private List<Evidence> CheckStatusCodes(HttpResponseData response)
    {
        var evidence = new List<Evidence>();

        if (response.StatusCode == 403 &&
            (response.Headers.ContainsKey("cf-ray") ||
             response.Body?.ToLower().Contains("cloudflare") == true))
        {
            _logger?.LogWarning("CloudFlare 403 Forbidden detected for {Url}", response.Url);
            evidence.Add(new Evidence
            {
                Method = DetectionMethod.StatusCode,
                Name = "status-code",
                Value = "403",
                Confidence = 0.75,
                Description = "CloudFlare 403 Forbidden response",
                Signature = "cf-403-status",
                Weight = 0.75
            });
        }

        if (response.StatusCode == 429 && response.Headers.ContainsKey("cf-ray"))
        {
            _logger?.LogWarning("CloudFlare rate limiting (429) detected for {Url}", response.Url);
            evidence.Add(new Evidence
            {
                Method = DetectionMethod.StatusCode,
                Name = "status-code",
                Value = "429",
                Confidence = 0.80,
                Description = "CloudFlare rate limiting detected",
                Signature = "cf-429-status",
                Weight = 0.80
            });
        }

        return evidence;
    }
}
