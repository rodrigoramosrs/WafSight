# Dependency Injection Integration

Integrate WafSight with ASP.NET Core dependency injection for scalable applications.

## Overview

WafSight provides first-class support for Microsoft.Extensions.DependencyInjection, making it easy to integrate into ASP.NET Core applications, background services, and any DI-enabled architecture.

## Installation

```bash
dotnet add package WafSight
```

## Basic Registration

### Minimal API (ASP.NET Core 6+)

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddWafDetector();

var app = builder.Build();

app.MapGet("/detect/{url}", async (string url, IWafDetector detector) =>
{
    var result = await detector.DetectAsync(url);
    return Results.Ok(result);
});

app.Run();
```

### Traditional Startup.cs

```csharp
public class Startup
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddWafDetector();
    }
}
```

## Configuration Options

### Configure Timeout

```csharp
services.AddWafDetector(options =>
{
    options.Timeout = TimeSpan.FromSeconds(30);
});
```

### Disable Features

```csharp
services.AddWafDetector(options =>
{
    options.EnableGenericDetection = false;  // Disable payload probing
    options.EnableDnsAnalysis = false;       // Disable DNS checks
});
```

### Complete Configuration

```csharp
services.AddWafDetector(options =>
{
    options.Timeout = TimeSpan.FromSeconds(15);
    options.EnableGenericDetection = true;
    options.EnableDnsAnalysis = true;
});
```

## Using in Controllers

### Inject IWafDetector

```csharp
using Microsoft.AspNetCore.Mvc;
using WafSight;

[ApiController]
[Route("api/[controller]")]
public class WafController : ControllerBase
{
    private readonly IWafDetector _detector;
    
    public WafController(IWafDetector detector)
    {
        _detector = detector;
    }
    
    [HttpGet("detect/{url}")]
    public async Task<ActionResult<DetectionResult>> Detect(string url)
    {
        var result = await _detector.DetectAsync(url);
        return Ok(result);
    }
    
    [HttpGet("providers")]
    public ActionResult<IEnumerable<ProviderMetadata>> Providers()
    {
        var client = (WafDetectorClient)_detector;
        return Ok(client.ListProviders());
    }
}
```

### Multiple Endpoints

```csharp
[ApiController]
[Route("api/waf")]
public class WafController : ControllerBase
{
    private readonly IWafDetector _detector;
    
    public WafController(IWafDetector detector)
    {
        _detector = detector;
    }
    
    [HttpGet("{url}")]
    public async Task<IActionResult> Detect(string url)
    {
        var result = await _detector.DetectAsync(url);
        return Ok(new
        {
            url,
            hasWaf = result.HasWaf,
            hasCdn = result.HasCdn,
            waf = result.Waf?.Name,
            cdn = result.Cdn?.Name,
            timeMs = result.DetectionTimeMs
        });
    }
    
    [HttpPost("batch")]
    public async Task<IActionResult> BatchDetect([FromBody] BatchRequest request)
    {
        var results = await _detector.DetectBatchAsync(request.Urls);
        return Ok(results);
    }
}

public class BatchRequest
{
    public string[] Urls { get; set; } = Array.Empty<string>();
}
```

## Background Services

### Simple Background Service

```csharp
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WafSight;

public class WafMonitoringService : BackgroundService
{
    private readonly IWafDetector _detector;
    private readonly ILogger<WafMonitoringService> _logger;
    
    public WafMonitoringService(IWafDetector detector, ILogger<WafMonitoringService> logger)
    {
        _detector = detector;
        _logger = logger;
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var targets = new[] { "https://example.com", "https://target.com" };
        
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var results = await _detector.DetectBatchAsync(targets);
                
                foreach (var (url, result) in results)
                {
                    if (result.HasWaf)
                    {
                        _logger.LogInformation(
                            "{Url} is protected by {Waf} ({Confidence:P0})",
                            url, result.Waf.Name, result.Waf.Confidence);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during WAF detection");
            }
            
            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
        }
    }
}
```

### Register Background Service

```csharp
services.AddHostedService<WafMonitoringService>();
```

## Custom Logger Integration

### With Serilog

```csharp
using Serilog;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .CreateLogger();

services.AddLogging(builder =>
{
    builder.AddSerilog();
});

services.AddWafDetector();
```

### With NLog

```csharp
using NLog.Web;

services.AddLogging(builder =>
{
    builder.AddNLogWeb();
});

services.AddWafDetector();
```

### Custom Filter

```csharp
services.AddWafDetector();

services.AddLogging(builder =>
{
    builder.AddFilter("WafSight.Http", LogLevel.Warning);
    builder.AddFilter("WafSight.Providers", LogLevel.Information);
});
```

## Multiple Instances

If you need multiple WafDetectorClient instances with different configurations:

```csharp
// Register named instances
services.AddSingleton<IWafDetector>(sp =>
{
    var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
    return new WafDetectorClient(loggerFactory, TimeSpan.FromSeconds(10));
});

services.AddSingleton<IWafDetector>("fast", sp =>
{
    var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
    return new WafDetectorClient(loggerFactory, TimeSpan.FromSeconds(5));
});
```

## Testing

### Mock IWafDetector

```csharp
using Moq;
using WafSight;

var mockDetector = new Mock<IWafDetector>();
mockDetector.Setup(d => d.DetectAsync(It.IsAny<string>()))
    .ReturnsAsync(new DetectionResult 
    { 
        Waf = new ProviderDetection { Name = "TestWAF", Confidence = 0.95 } 
    });

services.AddSingleton(mockDetector.Object);
```

### Integration Testing

```csharp
[Fact]
public async Task DetectAsync_WithRealProvider_ReturnsResult()
{
    var services = new ServiceCollection();
    services.AddWafDetector();
    var provider = services.BuildServiceProvider();
    
    var detector = provider.GetRequiredService<IWafDetector>();
    var result = await detector.DetectAsync("https://cloudflare.com");
    
    Assert.True(result.HasWaf);
    Assert.Equal("CloudFlare", result.Waf.Name);
}
```

## Best Practices

1. **Register once** - Use singleton lifetime (default)
2. **Configure at startup** - Set timeout and options in DI container
3. **Use interfaces** - Depend on `IWafDetector`, not concrete class
4. **Handle exceptions** - Network errors can occur during detection
5. **Log appropriately** - Use structured logging for better debugging

## See Also

- [Library Integration](library-integration.md) - Basic library usage
- [Custom Providers](custom-providers.md) - Add custom detection providers
- [API Reference](api-reference.md) - Complete API documentation
