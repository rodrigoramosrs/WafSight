using System.Text.RegularExpressions;
using WafSight.Models;

namespace WafSight.Providers;

/// <summary>
/// Sucuri WAF detection provider
/// </summary>
public class SucuriProvider : IDetectionProvider
{
    public string Name => "Sucuri";
    public string Version => "2.0.0";
    public string Description => "Sucuri WAF detection provider";
    public ProviderType ProviderType => ProviderType.WAF;
    public double ConfidenceBase => 0.88;
    public int Priority => 70;
    public bool Enabled => true;

    private static readonly Regex SucuriServerPattern = new(@"(?i)sucuri", RegexOptions.Compiled);
    private static readonly Regex SucuriChallengePattern = new(@"(?i)sucuri-cloudproxy|suscher|sucuri-website-firewall", RegexOptions.Compiled);
    private static readonly Regex SucuriNbsPattern = new(@"(?i)x-sucuri-id|x-sucuri-cache", RegexOptions.Compiled);

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

        if (response.Headers.TryGetValue("x-sucuri-id", out var sucuriId))
        {
            evidence.Add(new Evidence
            {
                Method = DetectionMethod.Header,
                Name = "x-sucuri-id",
                Value = sucuriId,
                Confidence = 0.95,
                Description = "Sucuri ID header detected",
                Signature = "x-sucuri-id-header",
                Weight = 1.0
            });
        }

        if (response.Headers.TryGetValue("x-sucuri-cache", out var sucuriCache))
        {
            evidence.Add(new Evidence
            {
                Method = DetectionMethod.Header,
                Name = "x-sucuri-cache",
                Value = sucuriCache,
                Confidence = 0.90,
                Description = "Sucuri cache header detected",
                Signature = "x-sucuri-cache-header",
                Weight = 0.90
            });
        }

        if (response.Headers.TryGetValue("server", out var server) &&
            SucuriServerPattern.IsMatch(server))
        {
            evidence.Add(new Evidence
            {
                Method = DetectionMethod.Header,
                Name = "server",
                Value = server,
                Confidence = 0.80,
                Description = "Sucuri server header detected",
                Signature = "sucuri-server-header",
                Weight = 0.80
            });
        }

        if (response.Headers.TryGetValue("x-sucuri-block", out var block))
        {
            evidence.Add(new Evidence
            {
                Method = DetectionMethod.Header,
                Name = "x-sucuri-block",
                Value = block,
                Confidence = 0.90,
                Description = "Sucuri block header detected",
                Signature = "x-sucuri-block-header",
                Weight = 0.90
            });
        }

        return evidence;
    }

    private List<Evidence> CheckCookies(HttpResponseData response)
    {
        var evidence = new List<Evidence>();

        if (response.Headers.TryGetValue("set-cookie", out var cookies))
        {
            if (cookies.Contains("sucuri") || cookies.Contains("sscf"))
            {
                evidence.Add(new Evidence
                {
                    Method = DetectionMethod.Cookie,
                    Name = "set-cookie",
                    Value = cookies,
                    Confidence = 0.85,
                    Description = "Sucuri cookie detected",
                    Signature = "sucuri-cookie",
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

        if (SucuriChallengePattern.IsMatch(body))
        {
            evidence.Add(new Evidence
            {
                Method = DetectionMethod.Body,
                Name = "challenge-page",
                Value = "detected",
                Confidence = 0.75,
                Description = "Sucuri challenge page detected",
                Signature = "sucuri-challenge-body",
                Weight = 0.50
            });
        }

        if (body.Contains("sucuri webserver firewall") || body.Contains("access denied"))
        {
            evidence.Add(new Evidence
            {
                Method = DetectionMethod.Body,
                Name = "body-content",
                Value = "sucuri detected",
                Confidence = 0.70,
                Description = "Sucuri content in response body",
                Signature = "sucuri-body",
                Weight = 0.50
            });
        }

        return evidence;
    }
}
