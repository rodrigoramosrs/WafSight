using Microsoft.Extensions.Logging;
using WafSight.Models;

namespace WafSight.Analysis;

/// <summary>
/// Generic WAF detection when no specific match is found
/// Based on the wafw00f approach
/// </summary>
public class GenericDetector
{
    private readonly EvidenceScorer _scorer = new();
    private readonly ILogger<GenericDetector>? _logger;

    public GenericDetector(ILogger<GenericDetector>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Attempts to detect a generic WAF by comparing normal vs attack responses
    /// </summary>
    public async Task<DetectionResult?> DetectGenericAsync(
        DetectionContext context,
        Func<string, Task<HttpResponseData?>> requestFunc)
    {
        if (context.Response == null)
            return null;

        _logger?.LogInformation("Starting generic WAF detection for {Url}", context.Url);

        var evidence = new List<Models.Evidence>();
        var reasons = new List<string>();

        var noUaResponse = await RequestWithoutUserAgent(context.Url, requestFunc);
        if (noUaResponse is not null && context.Response.StatusCode != noUaResponse.StatusCode)
        {
            _logger?.LogInformation("Generic detection: User-Agent difference detected");
            reasons.Add("Server returned a different response when request had no User-Agent");
            evidence.Add(new Models.Evidence
            {
                Method = Models.DetectionMethod.Header,
                Name = "User-Agent",
                Value = "missing",
                Confidence = 0.75,
                Description = "Response difference without User-Agent"
            });
        }

        var xssResponse = await RequestWithPayload(context.Url, "xss", requestFunc);
        if (xssResponse is not null && context.Response.StatusCode != xssResponse.StatusCode)
        {
            _logger?.LogInformation("Generic detection: XSS payload blocked");
            reasons.Add("Server returned a different response with XSS payload");
            evidence.Add(new Models.Evidence
            {
                Method = Models.DetectionMethod.Payload,
                Name = "xss-payload",
                Value = "<script>alert(1)</script>",
                Confidence = 0.80,
                Description = "XSS payload blocked"
            });
        }

        var sqliResponse = await RequestWithPayload(context.Url, "sqli", requestFunc);
        if (sqliResponse is not null && context.Response.StatusCode != sqliResponse.StatusCode)
        {
            _logger?.LogInformation("Generic detection: SQLi payload blocked");
            reasons.Add("Server returned a different response with SQLi payload");
            evidence.Add(new Models.Evidence
            {
                Method = Models.DetectionMethod.Payload,
                Name = "sqli-payload",
                Value = "' OR '1'='1",
                Confidence = 0.80,
                Description = "SQLi payload blocked"
            });
        }

        var lfiResponse = await RequestWithPayload(context.Url, "lfi", requestFunc);
        if (lfiResponse is not null && context.Response.StatusCode != lfiResponse.StatusCode)
        {
            _logger?.LogInformation("Generic detection: LFI payload blocked");
            reasons.Add("Server returned a different response with LFI payload");
            evidence.Add(new Models.Evidence
            {
                Method = Models.DetectionMethod.Payload,
                Name = "lfi-payload",
                Value = "../../etc/passwd",
                Confidence = 0.75,
                Description = "LFI payload blocked"
            });
        }

        if (context.Response.Headers.TryGetValue("Server", out var normalServer) &&
            xssResponse?.Headers.TryGetValue("Server", out var attackServer) == true &&
            normalServer != attackServer)
        {
            _logger?.LogInformation("Generic detection: Server header changed from '{Normal}' to '{Attack}'", normalServer, attackServer);
            reasons.Add($"Server header changed from '{normalServer}' to '{attackServer}'");
            evidence.Add(new Models.Evidence
            {
                Method = Models.DetectionMethod.Header,
                Name = "Server",
                Value = attackServer,
                Confidence = 0.85,
                Description = "Different Server header in attack response"
            });
        }

        if (xssResponse == null && context.Response is not null)
        {
            _logger?.LogInformation("Generic detection: Connection blocked during attack request");
            reasons.Add("Connection was blocked during attack request");
            evidence.Add(new Models.Evidence
            {
                Method = Models.DetectionMethod.Payload,
                Name = "connection-blocked",
                Value = "blocked",
                Confidence = 0.70,
                Description = "Connection blocked at packet/connection level"
            });
        }

        if (!evidence.Any())
        {
            _logger?.LogDebug("Generic detection: No evidence found for {Url}", context.Url);
            return null;
        }

        var confidence = _scorer.CalculateConfidence(evidence);
        _logger?.LogInformation("Generic detection scoring: {Count} evidence(s), confidence={Confidence}", evidence.Count, confidence);

        if (confidence < 0.60)
        {
            _logger?.LogDebug("Generic detection: Confidence {Confidence} below threshold for {Url}", confidence, context.Url);
            return null;
        }

        _logger?.LogInformation("Generic WAF detected for {Url} with confidence {Confidence}", context.Url, confidence);

        return new DetectionResult
        {
            Url = context.Url,
            Waf = new ProviderDetection
            {
                Name = "Generic WAF",
                Confidence = confidence
            },
            Evidence = evidence,
            Caveats = reasons
        };
    }

    private async Task<HttpResponseData?> RequestWithoutUserAgent(
        string url,
        Func<string, Task<HttpResponseData?>> requestFunc)
    {
        try
        {
            return await requestFunc(url + "?_wafdetect_noua=1");
        }
        catch
        {
            return null;
        }
    }

    private async Task<HttpResponseData?> RequestWithPayload(
        string url,
        string payloadType,
        Func<string, Task<HttpResponseData?>> requestFunc)
    {
        var payload = payloadType switch
        {
            "xss" => "<script>alert(1)</script>",
            "sqli" => "' OR '1'='1",
            "lfi" => "../../etc/passwd",
            "xxe" => "<!ENTITY xxe SYSTEM \"file:///etc/shadow\">",
            "rce" => "; cat /etc/passwd",
            _ => ""
        };

        try
        {
            var encodedPayload = Uri.EscapeDataString(payload);
            return await requestFunc($"{url}?_wafdetect_{payloadType}={encodedPayload}");
        }
        catch
        {
            return null;
        }
    }
}
