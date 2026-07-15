using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace WafSight.Extensions;

/// <summary>
/// Extensions for DI integration
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds WafSight to services
    /// </summary>
    public static IServiceCollection AddWafDetector(
        this IServiceCollection services,
        Action<WafDetectorOptions>? configureOptions = null)
    {
        var options = new WafDetectorOptions();
        configureOptions?.Invoke(options);

        services.AddSingleton(options);
        services.AddSingleton<IWafDetector>(sp =>
        {
            var logger = sp.GetService<ILogger<WafDetectorClient>>();
            return new WafDetectorClient(logger, options.Timeout);
        });

        return services;
    }
}

/// <summary>
/// WafSight configuration options
/// </summary>
public class WafDetectorOptions
{
    /// <summary>
    /// Timeout for HTTP requests
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Custom User-Agent
    /// </summary>
    public string? UserAgent { get; set; }

    /// <summary>
    /// Enable generic detection
    /// </summary>
    public bool EnableGenericDetection { get; set; } = true;

    /// <summary>
    /// Enable DNS analysis
    /// </summary>
    public bool EnableDnsAnalysis { get; set; } = true;
}
