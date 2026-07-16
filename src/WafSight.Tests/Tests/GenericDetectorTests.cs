using FluentAssertions;
using WafSight.Analysis;
using WafSight.Models;
using Xunit;

namespace WafSight.Tests.Tests;

public class GenericDetectorTests
{
    [Fact]
    public async Task DetectGenericAsync_NullResponse_ReturnsNull()
    {
        var detector = new GenericDetector();
        var context = new DetectionContext
        {
            Url = "https://example.com",
            Response = null
        };

        var result = await detector.DetectGenericAsync(context, _ => Task.FromResult<HttpResponseData?>(null));
        result.Should().BeNull();
    }

    [Fact]
    public async Task DetectGenericAsync_NoPayloadBlocked_ReturnsNull()
    {
        var detector = new GenericDetector();
        var context = new DetectionContext
        {
            Url = "https://example.com",
            Response = new HttpResponseData
            {
                StatusCode = 200,
                Headers = new Dictionary<string, string>
                {
                    { "server", "nginx" }
                }
            }
        };

        var result = await detector.DetectGenericAsync(context, _ =>
            Task.FromResult<HttpResponseData?>(new HttpResponseData
            {
                StatusCode = 200,
                Headers = new Dictionary<string, string>
                {
                    { "server", "nginx" }
                }
            }));

        result.Should().BeNull();
    }

    [Fact]
    public async Task DetectGenericAsync_ConnectionBlocked_ReturnsGenericDetection()
    {
        var detector = new GenericDetector();
        var context = new DetectionContext
        {
            Url = "https://example.com",
            Response = new HttpResponseData
            {
                StatusCode = 200,
                Headers = new Dictionary<string, string>
                {
                    { "server", "nginx" }
                }
            }
        };

        var result = await detector.DetectGenericAsync(context, _ =>
            Task.FromResult<HttpResponseData?>(null));

        result.Should().NotBeNull();
        result!.HasWaf.Should().BeTrue();
        result.Waf!.Name.Should().Be("Generic WAF");
    }

    [Fact]
    public async Task DetectGenericAsync_ServerHeaderChanged_ReturnsDetection()
    {
        var detector = new GenericDetector();
        var context = new DetectionContext
        {
            Url = "https://example.com",
            Response = new HttpResponseData
            {
                StatusCode = 200,
                Headers = new Dictionary<string, string>
                {
                    { "server", "nginx" }
                }
            }
        };

        var result = await detector.DetectGenericAsync(context, url =>
        {
            if (url.Contains("_wafdetect_xss"))
            {
                return Task.FromResult<HttpResponseData?>(new HttpResponseData
                {
                    StatusCode = 403,
                    Headers = new Dictionary<string, string>
                    {
                        { "server", "CloudFlare" }
                    }
                });
            }
            return Task.FromResult<HttpResponseData?>(null);
        });

        result.Should().NotBeNull();
        result!.HasWaf.Should().BeTrue();
    }
}
