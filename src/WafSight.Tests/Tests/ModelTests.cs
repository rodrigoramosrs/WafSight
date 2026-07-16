using FluentAssertions;
using WafSight.Models;
using Xunit;

namespace WafSight.Tests.Tests;

public class ModelTests
{
    [Fact]
    public void DetectionContext_DefaultValues()
    {
        var context = new DetectionContext();

        context.Url.Should().BeEmpty();
        context.Response.Should().BeNull();
        context.DnsInfo.Should().BeNull();
        context.UserAgent.Should().Be("WafSight/2.0");
        context.PayloadResponses.Should().BeEmpty();
    }

    [Fact]
    public void HttpResponseData_DefaultValues()
    {
        var response = new HttpResponseData();

        response.StatusCode.Should().Be(0);
        response.Headers.Should().BeEmpty();
        response.Body.Should().BeEmpty();
        response.Url.Should().BeEmpty();
        response.ResponseTime.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void DnsInfo_DefaultValues()
    {
        var dnsInfo = new DnsInfo();

        dnsInfo.Cnames.Should().BeEmpty();
        dnsInfo.ARecords.Should().BeEmpty();
        dnsInfo.NsRecords.Should().BeEmpty();
        dnsInfo.TxtRecords.Should().BeEmpty();
    }

    [Fact]
    public void Evidence_DefaultValues()
    {
        var evidence = new Evidence();

        evidence.Method.Should().Be(DetectionMethod.Header);
        evidence.Name.Should().BeEmpty();
        evidence.Value.Should().BeEmpty();
        evidence.Signature.Should().BeEmpty();
        evidence.Confidence.Should().Be(0.0);
        evidence.Description.Should().BeEmpty();
        evidence.Weight.Should().Be(1.0);
    }

    [Fact]
    public void ProviderType_AllValuesExist()
    {
        var values = Enum.GetValues<ProviderType>();

        values.Should().Contain(ProviderType.WAF);
        values.Should().Contain(ProviderType.CDN);
        values.Should().Contain(ProviderType.Both);
    }

    [Fact]
    public void ProviderType_FlagsWork()
    {
        var both = ProviderType.Both;

        both.HasFlag(ProviderType.WAF).Should().BeTrue();
        both.HasFlag(ProviderType.CDN).Should().BeTrue();
        (both | ProviderType.WAF).Should().Be(ProviderType.Both);
    }

    [Fact]
    public void DetectionMethod_AllValuesExist()
    {
        var values = Enum.GetValues<DetectionMethod>();

        values.Should().Contain(DetectionMethod.Header);
        values.Should().Contain(DetectionMethod.Body);
        values.Should().Contain(DetectionMethod.StatusCode);
        values.Should().Contain(DetectionMethod.DNS);
        values.Should().Contain(DetectionMethod.Timing);
        values.Should().Contain(DetectionMethod.Certificate);
        values.Should().Contain(DetectionMethod.Cookie);
        values.Should().Contain(DetectionMethod.Payload);
    }
}
