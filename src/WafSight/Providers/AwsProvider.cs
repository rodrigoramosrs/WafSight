using System.Text.RegularExpressions;
using WafSight.Models;

namespace WafSight.Providers;

/// <summary>
/// AWS WAF and CloudFront CDN detection provider
/// </summary>
public class AwsProvider : IDetectionProvider
{
    public string Name => "AWS";
    public string Version => "2.0.0";
    public string Description => "AWS WAF and CloudFront CDN detection provider";
    public ProviderType ProviderType => ProviderType.Both;
    public double ConfidenceBase => 0.90;
    public int Priority => 95;
    public bool Enabled => true;

    private static readonly Regex CloudFrontIdPattern = new(@"^[A-Za-z0-9]{8}-[A-Za-z0-9]{4}-[A-Za-z0-9]{4}-[A-Za-z0-9]{4}-[A-Za-z0-9]{12}$", RegexOptions.Compiled);
    private static readonly Regex CloudFrontPopPattern = new(@"^[A-Z]{3}[0-9]+-[A-Z][0-9]+$", RegexOptions.Compiled);
    private static readonly Regex CloudFrontViaPattern = new(@"(?i)(cloudfront|1\.1 [a-f0-9]+ \(CloudFront\))", RegexOptions.Compiled);

    public async Task<List<Evidence>> DetectAsync(DetectionContext context)
    {
        var evidence = new List<Evidence>();

        if (context.Response is not null)
        {
            evidence.AddRange(await CheckHeaders(context.Response));
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

        if (response.Headers.TryGetValue("x-amz-cf-id", out var cfId) &&
            CloudFrontIdPattern.IsMatch(cfId))
        {
            evidence.Add(new Evidence
            {
                Method = DetectionMethod.Header,
                Name = "x-amz-cf-id",
                Value = cfId,
                Confidence = 0.95,
                Description = "CloudFront request ID header detected",
                Signature = "x-amz-cf-id-header",
                Weight = 1.0
            });
        }

        if (response.Headers.TryGetValue("x-amz-cf-pop", out var cfPop) &&
            CloudFrontPopPattern.IsMatch(cfPop))
        {
            evidence.Add(new Evidence
            {
                Method = DetectionMethod.Header,
                Name = "x-amz-cf-pop",
                Value = cfPop,
                Confidence = 0.90,
                Description = "CloudFront Point of Presence header detected",
                Signature = "x-amz-cf-pop-header",
                Weight = 0.95
            });
        }

        if (response.Headers.TryGetValue("via", out var via) &&
            CloudFrontViaPattern.IsMatch(via))
        {
            evidence.Add(new Evidence
            {
                Method = DetectionMethod.Header,
                Name = "via",
                Value = via,
                Confidence = 0.85,
                Description = "CloudFront via header detected",
                Signature = "cloudfront-via-header",
                Weight = 0.90
            });
        }

        if (response.Headers.TryGetValue("x-cache", out var cache) &&
            cache.ToLower().Contains("cloudfront"))
        {
            evidence.Add(new Evidence
            {
                Method = DetectionMethod.Header,
                Name = "x-cache",
                Value = cache,
                Confidence = 0.80,
                Description = "CloudFront cache header detected",
                Signature = "cloudfront-cache-pattern",
                Weight = 0.85
            });
        }

        if (response.Headers.TryGetValue("x-amzn-requestid", out var requestId))
        {
            evidence.Add(new Evidence
            {
                Method = DetectionMethod.Header,
                Name = "x-amzn-requestid",
                Value = requestId,
                Confidence = 0.85,
                Description = "AWS request ID header detected",
                Signature = "aws-request-id-pattern",
                Weight = 0.90
            });
        }

        return Task.FromResult(evidence);
    }
}
