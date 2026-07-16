using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using WafSight.Models;

namespace WafSight.Providers;

/// <summary>
/// Microsoft Azure WAF/CDN detection provider
/// </summary>
public class AzureProvider : IDetectionProvider
{
    public string Name => "Azure";
    public string Version => "2.0.0";
    public string Description => "Microsoft Azure WAF and CDN detection provider";
    public ProviderType ProviderType => ProviderType.Both;
    public double ConfidenceBase => 0.88;
    public int Priority => 80;
    public bool Enabled => true;

    private readonly ILogger<AzureProvider>? _logger;

    public AzureProvider(ILogger<AzureProvider>? logger = null)
    {
        _logger = logger;
    }

    private static readonly Regex AzurePmoPattern = new(@"^AzurePmo", RegexOptions.Compiled);
    private static readonly Regex AzureFrontDoorPattern = new(@"(?i)azure[-_]front[-_]door", RegexOptions.Compiled);
    private static readonly Regex AzureApplicationGatewayPattern = new(@"(?i)application[-_]gateway", RegexOptions.Compiled);
    private static readonly Regex AzureNrpPattern = new(@"(?i)x-azure-ref", RegexOptions.Compiled);
    private static readonly Regex AzureTunePattern = new(@"^AzureTune", RegexOptions.Compiled);
    private static readonly Regex AzureWafPattern = new(@"(?i)x-aspnetcore-requestcontext", RegexOptions.Compiled);

    public async Task<List<Evidence>> DetectAsync(DetectionContext context)
    {
        _logger?.LogDebug("Starting Azure detection for {Url}", context.Url);
        var evidence = new List<Evidence>();

        if (context.Response is not null)
        {
            evidence.AddRange(await CheckHeaders(context.Response));
            _logger?.LogInformation("Azure evidence found: {Evidence}", evidence.Count > 0 ? evidence.LastOrDefault()?.Name ?? "unknown" : "none");
            _logger?.LogDebug("Azure detection completed: {Count} evidence(s)", evidence.Count);
        }
        else
        {
            _logger?.LogWarning("No response data for Azure detection on {Url}", context.Url);
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

        if (response.Headers.TryGetValue("x-azure-ref", out var azureRef))
        {
            evidence.Add(new Evidence
            {
                Method = DetectionMethod.Header,
                Name = "x-azure-ref",
                Value = azureRef,
                Confidence = 0.95,
                Description = "Azure reference header detected",
                Signature = "x-azure-ref-header",
                Weight = 1.0
            });
        }

        if (response.Headers.TryGetValue("x-aspnetcore-requestcontext", out var requestContext))
        {
            evidence.Add(new Evidence
            {
                Method = DetectionMethod.Header,
                Name = "x-aspnetcore-requestcontext",
                Value = requestContext,
                Confidence = 0.80,
                Description = "ASP.NET Core request context header detected",
                Signature = "aspnetcore-requestcontext-header",
                Weight = 0.80
            });
        }

        if (response.Headers.TryGetValue("server", out var server))
        {
            if (AzureApplicationGatewayPattern.IsMatch(server))
            {
                evidence.Add(new Evidence
                {
                    Method = DetectionMethod.Header,
                    Name = "server",
                    Value = server,
                    Confidence = 0.85,
                    Description = "Azure Application Gateway server header detected",
                    Signature = "azure-application-gateway-header",
                    Weight = 0.85
                });
            }
        }

        if (response.Headers.TryGetValue("x-arr-log-id", out var arrLogId))
        {
            evidence.Add(new Evidence
            {
                Method = DetectionMethod.Header,
                Name = "x-arr-log-id",
                Value = arrLogId,
                Confidence = 0.85,
                Description = "Azure Application Request Routing log ID header detected",
                Signature = "x-arr-log-id-header",
                Weight = 0.85
            });
        }

        if (response.Headers.TryGetValue("x-cache", out var cache) &&
            cache.Contains("CONFIGURING", StringComparison.OrdinalIgnoreCase))
        {
            evidence.Add(new Evidence
            {
                Method = DetectionMethod.Header,
                Name = "x-cache",
                Value = cache,
                Confidence = 0.80,
                Description = "Azure CDN cache status header detected",
                Signature = "azure-cdn-cache-header",
                Weight = 0.80
            });
        }

        return Task.FromResult(evidence);
    }
}
