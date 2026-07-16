using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using WafSight;
using WafSight.Extensions;
using Xunit;

namespace WafSight.Tests.Integration;

public class DiIntegrationTests
{
    [Fact]
    public void AddWafDetector_RegistersService()
    {
        var services = new ServiceCollection();
        services.AddWafDetector();

        var provider = services.BuildServiceProvider();
        var detector = provider.GetService<IWafDetector>();

        detector.Should().NotBeNull();
        detector.Should().BeOfType<WafDetectorClient>();
    }

    [Fact]
    public void AddWafDetector_WithCustomOptions_AppliesOptions()
    {
        var services = new ServiceCollection();
        services.AddWafDetector(options =>
        {
            options.Timeout = TimeSpan.FromSeconds(30);
            options.EnableGenericDetection = false;
            options.EnableDnsAnalysis = false;
        });

        var provider = services.BuildServiceProvider();
        var options = provider.GetService<WafDetectorOptions>();

        options.Should().NotBeNull();
        options!.Timeout.Should().Be(TimeSpan.FromSeconds(30));
        options.EnableGenericDetection.Should().BeFalse();
        options.EnableDnsAnalysis.Should().BeFalse();
    }

    [Fact]
    public void AddWafDetector_DefaultOptions_HasCorrectDefaults()
    {
        var services = new ServiceCollection();
        services.AddWafDetector();

        var provider = services.BuildServiceProvider();
        var options = provider.GetService<WafDetectorOptions>();

        options.Should().NotBeNull();
        options!.Timeout.Should().Be(TimeSpan.FromSeconds(10));
        options.EnableGenericDetection.Should().BeTrue();
        options.EnableDnsAnalysis.Should().BeTrue();
        options.UserAgent.Should().BeNull();
    }

    [Fact]
    public void AddWafDetector_CanBeResolvedMultipleTimes()
    {
        var services = new ServiceCollection();
        services.AddWafDetector();

        var provider = services.BuildServiceProvider();
        var detector1 = provider.GetService<IWafDetector>();
        var detector2 = provider.GetService<IWafDetector>();

        detector1.Should().NotBeNull();
        detector2.Should().NotBeNull();
        ReferenceEquals(detector1, detector2).Should().BeTrue();
    }
}

public class CliIntegrationTests
{
    [Fact]
    public async Task Cli_ShowsHelp_WhenNoArgs()
    {
        var stdout = Console.Out;
        using var sw = new StringWriter();
        Console.SetOut(sw);

        await WafSight.Cli.Program.Main(Array.Empty<string>());

        var output = sw.ToString();
        Console.SetOut(stdout);

        output.Should().Contain("Usage: waf-sight");
        output.Should().Contain("detect");
        output.Should().Contain("batch");
        output.Should().Contain("providers");
    }

    [Fact]
    public async Task Cli_ShowsVersion_WhenVersionFlag()
    {
        var stdout = Console.Out;
        using var sw = new StringWriter();
        Console.SetOut(sw);

        await WafSight.Cli.Program.Main(new[] { "--version" });

        var output = sw.ToString();
        Console.SetOut(stdout);

        output.Should().Contain("WafSight CLI v");
    }

    [Fact]
    public async Task Cli_ListsProviders_WhenProvidersFlag()
    {
        var stdout = Console.Out;
        using var sw = new StringWriter();
        Console.SetOut(sw);

        await WafSight.Cli.Program.Main(new[] { "-V", "1", "--providers" });

        var output = sw.ToString();
        Console.SetOut(stdout);

        output.Should().Contain("CloudFlare");
        output.Should().Contain("AWS");
        output.Should().Contain("Akamai");
        output.Should().Contain("Fastly");
        output.Should().Contain("Azure");
        output.Should().Contain("Imperva");
        output.Should().Contain("Sucuri");
        output.Should().Contain("F5");
        output.Should().Contain("Total:");
    }
}
