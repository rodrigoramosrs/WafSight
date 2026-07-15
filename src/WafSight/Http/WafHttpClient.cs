using System.Diagnostics;
using System.Net;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;
using WafSight.Models;

namespace WafSight.Http;

/// <summary>
/// HTTP client optimized for WAF detection
/// </summary>
public class WafHttpClient : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<WafHttpClient>? _logger;
    private readonly ResiliencePipeline<HttpResponseMessage> _resiliencePipeline;

    public WafHttpClient(
        ILogger<WafHttpClient>? logger = null,
        TimeSpan? timeout = null)
    {
        _logger = logger;

        var handler = new HttpClientHandler
        {
            AllowAutoRedirect = false,
            AutomaticDecompression = DecompressionMethods.All,
            ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
        };

        var resolvedTimeout = timeout ?? TimeSpan.FromSeconds(10);
        _httpClient = new HttpClient(handler) { Timeout = resolvedTimeout };
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "WafSight/2.0");
        _httpClient.DefaultRequestHeaders.Add("Accept", "*/*");
        _httpClient.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.9");

        _resiliencePipeline = BuildResiliencePipeline();
    }

    public WafHttpClient(
        HttpMessageHandler handler,
        ILogger<WafHttpClient>? logger = null,
        TimeSpan? timeout = null)
    {
        _logger = logger;

        var resolvedTimeout = timeout ?? TimeSpan.FromSeconds(10);
        _httpClient = new HttpClient(handler) { Timeout = resolvedTimeout };
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "WafSight/2.0");
        _httpClient.DefaultRequestHeaders.Add("Accept", "*/*");
        _httpClient.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.9");

        _resiliencePipeline = BuildResiliencePipeline();
    }

    private static ResiliencePipeline<HttpResponseMessage> BuildResiliencePipeline()
    {
        return new ResiliencePipelineBuilder<HttpResponseMessage>()
            .AddRetry(new RetryStrategyOptions<HttpResponseMessage>
            {
                MaxRetryAttempts = 2,
                Delay = TimeSpan.FromMilliseconds(500),
                BackoffType = DelayBackoffType.Exponential,
                ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                    .Handle<HttpRequestException>()
                    .Handle<TaskCanceledException>()
                    .HandleResult(r => r.StatusCode >= HttpStatusCode.InternalServerError)
            })
            .AddTimeout(TimeSpan.FromSeconds(15))
            .Build();
    }

    /// <summary>
    /// Sends a GET request
    /// </summary>
    public async Task<HttpResponseData?> GetAsync(string url, CancellationToken cancellationToken = default)
    {
        try
        {
            var sw = Stopwatch.StartNew();

            var response = await _resiliencePipeline.ExecuteAsync(
                async ct => await _httpClient.GetAsync(url, ct),
                cancellationToken);

            sw.Stop();

            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var header in response.Headers)
            {
                headers[header.Key] = string.Join(", ", header.Value);
            }

            foreach (var header in response.Content.Headers)
            {
                headers[header.Key] = string.Join(", ", header.Value);
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            return new HttpResponseData
            {
                StatusCode = (int)response.StatusCode,
                Headers = headers,
                Body = body,
                Url = url,
                ResponseTime = sw.Elapsed
            };
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Request failed for {Url}", url);
            return null;
        }
    }

    /// <summary>
    /// Sends a GET request with custom headers
    /// </summary>
    public async Task<HttpResponseData?> GetWithHeadersAsync(
        string url,
        Dictionary<string, string> headers,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var sw = Stopwatch.StartNew();

            var request = new HttpRequestMessage(HttpMethod.Get, url);
            foreach (var header in headers)
            {
                request.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            var response = await _resiliencePipeline.ExecuteAsync(
                async ct => await _httpClient.SendAsync(request, ct),
                cancellationToken);

            sw.Stop();

            var responseHeaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var header in response.Headers)
            {
                responseHeaders[header.Key] = string.Join(", ", header.Value);
            }

            foreach (var header in response.Content.Headers)
            {
                responseHeaders[header.Key] = string.Join(", ", header.Value);
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            return new HttpResponseData
            {
                StatusCode = (int)response.StatusCode,
                Headers = responseHeaders,
                Body = body,
                Url = url,
                ResponseTime = sw.Elapsed
            };
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Request with headers failed for {Url}", url);
            return null;
        }
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}
