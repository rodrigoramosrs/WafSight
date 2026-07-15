using System.Text.RegularExpressions;
using WafSight.Models;

namespace WafSight.Providers;

/// <summary>
/// Imperva (Incapsula) WAF detection provider
/// </summary>
public class ImpervaProvider : IDetectionProvider
{
    public string Name => "Imperva";
    public string Version => "2.0.0";
    public string Description => "Imperva (Incapsula) WAF detection provider";
    public ProviderType ProviderType => ProviderType.WAF;
    public double ConfidenceBase => 0.92;
    public int Priority => 75;
    public bool Enabled => true;

    private static readonly Regex ImpervaServerPattern = new(@"(?i)incap-s-info|incapsula", RegexOptions.Compiled);
    private static readonly Regex ImpervaCpCaptchaPattern = new(@"(?i)incap_ses|visid_incap|nlb_cpt|incapsula_incapsula", RegexOptions.Compiled);
    private static readonly Regex ImpervaNlbCptPattern = new(@"(?i)nlb_cpt|visid_incap", RegexOptions.Compiled);

    public Task<List<Evidence>> DetectAsync(DetectionContext context)
    {
        var evidence = new List<Evidence>();

        if (context.Response is not null)
        {
            evidence.AddRange(CheckHeaders(context.Response));
            evidence.AddRange(CheckCookies(context.Response));
            evidence.AddRange(CheckBody(context.Response));
        }

        return Task.FromResult(evidence);
    }

    public Task<List<Evidence>> PassiveDetectAsync(HttpResponseData response)
    {
        var evidence = new List<Evidence>();
        evidence.AddRange(CheckHeaders(response));
        evidence.AddRange(CheckCookies(response));
        evidence.AddRange(CheckBody(response));
        return Task.FromResult(evidence);
    }

    private List<Evidence> CheckHeaders(HttpResponseData response)
    {
        var evidence = new List<Evidence>();

        if (response.Headers.TryGetValue("x-ine-debug", out var ineDebug))
        {
            evidence.Add(new Evidence
            {
                Method = DetectionMethod.Header,
                Name = "x-ine-debug",
                Value = ineDebug,
                Confidence = 0.90,
                Description = "Imperva debug header detected",
                Signature = "x-ine-debug-header",
                Weight = 0.90
            });
        }

        if (response.Headers.TryGetValue("x-cdn", out var xCdn))
        {
            if (xCdn.Equals("INCAP", StringComparison.OrdinalIgnoreCase) ||
                xCdn.Equals("incapsula", StringComparison.OrdinalIgnoreCase))
            {
                evidence.Add(new Evidence
                {
                    Method = DetectionMethod.Header,
                    Name = "x-cdn",
                    Value = xCdn,
                    Confidence = 0.90,
                    Description = "Imperva CDN header detected",
                    Signature = "x-cdn-header",
                    Weight = 0.90
                });
            }
        }

        if (response.Headers.TryGetValue("server", out var server) &&
            ImpervaServerPattern.IsMatch(server))
        {
            evidence.Add(new Evidence
            {
                Method = DetectionMethod.Header,
                Name = "server",
                Value = server,
                Confidence = 0.85,
                Description = "Imperva server header detected",
                Signature = "imperva-server-header",
                Weight = 0.85
            });
        }

        if (response.Headers.TryGetValue("x-cache", out var cache))
        {
            if (cache.Equals("HIT", StringComparison.OrdinalIgnoreCase) &&
                response.Headers.TryGetValue("server", out _))
            {
                evidence.Add(new Evidence
                {
                    Method = DetectionMethod.Header,
                    Name = "x-cache",
                    Value = cache,
                    Confidence = 0.75,
                    Description = "Imperva cache status header detected",
                    Signature = "imperva-cache-header",
                    Weight = 0.75
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
            if (ImpervaCpCaptchaPattern.IsMatch(cookies))
            {
                evidence.Add(new Evidence
                {
                    Method = DetectionMethod.Cookie,
                    Name = "set-cookie",
                    Value = cookies,
                    Confidence = 0.90,
                    Description = "Imperva captcha cookie detected",
                    Signature = "imperva-captcha-cookie",
                    Weight = 0.90
                });
            }
        }

        return evidence;
    }

    private List<Evidence> CheckBody(HttpResponseData response)
    {
        var evidence = new List<Evidence>();
        var body = response.Body?.ToLower() ?? "";

        if (body.Contains("incap_ses") || body.Contains("incapsula") || body.Contains("incapacitor"))
        {
            evidence.Add(new Evidence
            {
                Method = DetectionMethod.Body,
                Name = "body-content",
                Value = "imperva detected",
                Confidence = 0.70,
                Description = "Imperva content in response body",
                Signature = "imperva-body",
                Weight = 0.50
            });
        }

        return evidence;
    }
}
