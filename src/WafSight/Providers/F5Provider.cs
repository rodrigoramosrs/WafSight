using System.Text.RegularExpressions;
using WafSight.Models;

namespace WafSight.Providers;

/// <summary>
/// F5 BIG-IP WAF detection provider
/// </summary>
public class F5Provider : IDetectionProvider
{
    public string Name => "F5";
    public string Version => "2.0.0";
    public string Description => "F5 BIG-IP WAF detection provider";
    public ProviderType ProviderType => ProviderType.WAF;
    public double ConfidenceBase => 0.88;
    public int Priority => 65;
    public bool Enabled => true;

    private static readonly Regex F5ServerPattern = new(@"(?i)f5 networks|big.?ip", RegexOptions.Compiled);
    private static readonly Regex F5TmCookiePattern = new(@"(?i)f5.?tm.?cookie", RegexOptions.Compiled);
    private static readonly Regex F5BigipPattern = new(@"(?i)x-ultimate-cp-id|x-ultimate-cp-id", RegexOptions.Compiled);
    private static readonly Regex F5VhostPattern = new(@"(?i)vhost", RegexOptions.Compiled);

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

        if (response.Headers.TryGetValue("server", out var server) &&
            F5ServerPattern.IsMatch(server))
        {
            evidence.Add(new Evidence
            {
                Method = DetectionMethod.Header,
                Name = "server",
                Value = server,
                Confidence = 0.85,
                Description = "F5 BIG-IP server header detected",
                Signature = "f5-bigip-header",
                Weight = 0.85
            });
        }

        if (response.Headers.TryGetValue("x-ultimate-cp-id", out var ultimateCpId))
        {
            evidence.Add(new Evidence
            {
                Method = DetectionMethod.Header,
                Name = "x-ultimate-cp-id",
                Value = ultimateCpId,
                Confidence = 0.90,
                Description = "F5 ultimate CP ID header detected",
                Signature = "x-ultimate-cp-id-header",
                Weight = 0.90
            });
        }

        if (response.Headers.TryGetValue("x-waf-request-id", out var wafRequestId))
        {
            evidence.Add(new Evidence
            {
                Method = DetectionMethod.Header,
                Name = "x-waf-request-id",
                Value = wafRequestId,
                Confidence = 0.85,
                Description = "F5 WAF request ID header detected",
                Signature = "x-waf-request-id-header",
                Weight = 0.85
            });
        }

        if (response.Headers.TryGetValue("x-cache", out var cache))
        {
            if (cache.Equals("HIT", StringComparison.OrdinalIgnoreCase) &&
                response.Headers.TryGetValue("server", out var srv) &&
                srv.Contains("bigip", StringComparison.OrdinalIgnoreCase))
            {
                evidence.Add(new Evidence
                {
                    Method = DetectionMethod.Header,
                    Name = "x-cache",
                    Value = cache,
                    Confidence = 0.75,
                    Description = "F5 BIG-IP cache header detected",
                    Signature = "f5-cache-header",
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
            if (F5TmCookiePattern.IsMatch(cookies))
            {
                evidence.Add(new Evidence
                {
                    Method = DetectionMethod.Cookie,
                    Name = "set-cookie",
                    Value = cookies,
                    Confidence = 0.85,
                    Description = "F5 BIG-IP cookie detected",
                    Signature = "f5-bigip-cookie",
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

        if (body.Contains("bigip") || body.Contains("f5 networks") || body.Contains("tmaccess"))
        {
            evidence.Add(new Evidence
            {
                Method = DetectionMethod.Body,
                Name = "body-content",
                Value = "f5 detected",
                Confidence = 0.70,
                Description = "F5 content in response body",
                Signature = "f5-body",
                Weight = 0.50
            });
        }

        return evidence;
    }
}
