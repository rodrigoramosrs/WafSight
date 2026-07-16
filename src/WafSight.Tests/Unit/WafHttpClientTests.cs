using System.Net;
using System.Text;
using FluentAssertions;
using WafSight.Http;
using Xunit;

namespace WafSight.Tests.Unit;

/// <summary>
/// Tests for WafHttpClient resilience, timeouts, and headers
/// </summary>
public class WafHttpClientTests : IDisposable
{
    [Fact]
    public async Task GetAsync_ReturnsResponseData_WithValidResponse()
    {
        var handler = CreateMockHandler(200, new Dictionary<string, string>
        {
            { "server", "nginx" },
            { "content-type", "text/html" }
        }, "<html>OK</html>");

        using var client = new WafHttpClient(handler, timeout: TimeSpan.FromSeconds(5));

        var response = await client.GetAsync("https://example.com");

        response.Should().NotBeNull();
        response!.StatusCode.Should().Be(200);
        response.Headers.Should().ContainKey("server");
        response.Body.Should().Contain("OK");
        response.Url.Should().Be("https://example.com");
    }

    [Fact]
    public async Task GetAsync_ReturnsNull_WhenRequestFails()
    {
        var handler = new FailingHttpMessageHandler();

        using var client = new WafHttpClient(handler, timeout: TimeSpan.FromSeconds(5));

        var response = await client.GetAsync("https://invalid.example.com");

        response.Should().BeNull();
    }

    [Fact]
    public async Task GetAsync_WithCustomTimeout_AppliesTimeout()
    {
        var handler = new SlowHttpMessageHandler(TimeSpan.FromSeconds(1));

        using var client = new WafHttpClient(handler, timeout: TimeSpan.FromMilliseconds(100));

        var response = await client.GetAsync("https://slow.example.com");

        response.Should().BeNull();
    }

    [Fact]
    public async Task GetWithHeadersAsync_SendsCustomHeaders()
    {
        var capturedHeaders = new Dictionary<string, string>();
        var handler = new CapturingHttpMessageHandler(capturedHeaders);

        using var client = new WafHttpClient(handler, timeout: TimeSpan.FromSeconds(5));

        var customHeaders = new Dictionary<string, string>
        {
            { "X-Custom-Header", "test-value" },
            { "X-Another-Header", "another-value" }
        };

        var response = await client.GetWithHeadersAsync("https://example.com", customHeaders);

        response.Should().NotBeNull();
        capturedHeaders.Should().ContainKey("X-Custom-Header");
        capturedHeaders["X-Custom-Header"].Should().Be("test-value");
    }

    [Fact]
    public async Task GetAsync_DefaultHeaders_AreSet()
    {
        var handler = new CapturingHttpMessageHandler(new Dictionary<string, string>());

        using var client = new WafHttpClient(handler, timeout: TimeSpan.FromSeconds(5));

        await client.GetAsync("https://example.com");

        handler.CapturedHeaders.Should().ContainKey("User-Agent");
        handler.CapturedHeaders["User-Agent"].Should().Be("WafSight/2.0");
    }

    [Fact]
    public async Task GetAsync_ServerError_StillReturnsResponse()
    {
        var handler = CreateMockHandler(500, new Dictionary<string, string>(), "Internal Server Error");

        using var client = new WafHttpClient(handler, timeout: TimeSpan.FromSeconds(5));

        var response = await client.GetAsync("https://example.com");

        response.Should().NotBeNull();
        response!.StatusCode.Should().Be(500);
    }

    [Fact]
    public async Task GetAsync_NotFound_Returns404()
    {
        var handler = CreateMockHandler(404, new Dictionary<string, string>(), "Not Found");

        using var client = new WafHttpClient(handler, timeout: TimeSpan.FromSeconds(5));

        var response = await client.GetAsync("https://example.com/missing");

        response.Should().NotBeNull();
        response!.StatusCode.Should().Be(404);
    }

    private HttpMessageHandler CreateMockHandler(int statusCode, Dictionary<string, string> headers, string body)
    {
        var handler = new MockHttpMessageHandler(statusCode, headers, body);
        return handler;
    }

    public void Dispose()
    {
    }

    private class FailingHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            throw new HttpRequestException("Connection failed");
        }
    }

    private class SlowHttpMessageHandler : HttpMessageHandler
    {
        private readonly TimeSpan _delay;

        public SlowHttpMessageHandler(TimeSpan delay)
        {
            _delay = delay;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<HttpResponseMessage>();
            Task.Delay(_delay, cancellationToken).ContinueWith(_ =>
            {
                tcs.TrySetException(new TaskCanceledException("Request timed out"));
            });
            return tcs.Task;
        }
    }

    private class CapturingHttpMessageHandler : HttpMessageHandler
    {
        private readonly Dictionary<string, string> _capturedHeaders;

        public CapturingHttpMessageHandler(Dictionary<string, string> capturedHeaders)
        {
            _capturedHeaders = capturedHeaders;
        }

        public Dictionary<string, string> CapturedHeaders => _capturedHeaders;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            foreach (var header in request.Headers)
            {
                _capturedHeaders[header.Key] = string.Join(", ", header.Value);
            }

            var response = new HttpResponseMessage(HttpStatusCode.OK);
            response.Content = new StringContent("OK", Encoding.UTF8, "text/plain");
            response.RequestMessage = request;

            return Task.FromResult(response);
        }
    }

    internal class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly int _statusCode;
        private readonly Dictionary<string, string> _headers;
        private readonly string _body;

        public MockHttpMessageHandler(int statusCode, Dictionary<string, string> headers, string body)
        {
            _statusCode = statusCode;
            _headers = headers;
            _body = body;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage((HttpStatusCode)_statusCode);

            foreach (var header in _headers)
            {
                response.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            response.Content = new StringContent(_body, Encoding.UTF8, "text/html");
            response.RequestMessage = request;

            return Task.FromResult(response);
        }
    }
}
