# Library Integration

Complete guide for integrating WafSight as a DLL in your .NET applications.

## Overview

WafSight can be used as a library in any .NET application:
- **Console applications**
- **ASP.NET Core web apps**
- **Background services**
- **Microservices**
- **Desktop applications**

## Installation

```bash
dotnet add package WafSight
```

## Basic Usage

### Simple Detection

```csharp
using WafSight;

var client = new WafDetectorClient();
var result = await client.DetectAsync("https://example.com");

Console.WriteLine($"WAF: {result.Waf?.Name ?? "Not detected"}");
Console.WriteLine($"CDN: {result.Cdn?.Name ?? "Not detected"}");
```

### With Error Handling

```csharp
using WafSight;

try
{
    var client = new WafDetectorClient();
    var result = await client.DetectAsync("https://example.com");
    
    if (result.HasWaf)
    {
        Console.WriteLine($"Protected by: {result.Waf.Name}");
    }
    
    if (result.HasCdn)
    {
        Console.WriteLine($"CDN: {result.Cdn.Name}");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"Detection failed: {ex.Message}");
}
```

## Advanced Usage

### Custom Timeout

```csharp
using WafSight;

var client = new WafDetectorClient(timeout: TimeSpan.FromSeconds(30));
var result = await client.DetectAsync("https://slow-server.com");
```

### With Logging

```csharp
using Microsoft.Extensions.Logging;
using WafSight;

var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddConsole();
    builder.SetMinimumLevel(LogLevel.Information);
});

var client = new WafDetectorClient(loggerFactory);
var result = await client.DetectAsync("https://example.com");
```

### Custom Logger Categories

```csharp
var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddConsole();
    builder.AddFilter("WafSight.Http", LogLevel.Warning);
    builder.AddFilter("WafSight.Providers", LogLevel.Information);
});

var client = new WafDetectorClient(loggerFactory);
```

## Batch Detection

### Basic Batch

```csharp
using WafSight;

var client = new WafDetectorClient();
var urls = new[]
{
    "https://example.com",
    "https://cloudflare.com",
    "https://aws.amazon.com"
};

var results = await client.DetectBatchAsync(urls);

foreach (var (url, result) in results)
{
    Console.WriteLine($"{url}: {result.Waf?.Name ?? "None"}");
}
```

### Controlled Concurrency

```csharp
using WafSight;

var client = new WafDetectorClient();
var urls = File.ReadAllLines("targets.txt");

// Process 5 URLs at a time
var results = await client.DetectBatchAsync(
    urls, 
    maxConcurrency: 5
);

Console.WriteLine($"Processed {results.Count} URLs");
```

### With Progress Reporting

```csharp
using WafSight;

var client = new WafDetectorClient();
var urls = File.ReadAllLines("targets.txt");
var processed = 0;

var results = await client.DetectBatchAsync(urls, maxConcurrency: 10);

foreach (var (url, result) in results)
{
    processed++;
    Console.WriteLine($"[{processed}/{urls.Length}] {url}: {result.Waf?.Name ?? "None"}");
}
```

## Batch from File

```csharp
using WafSight;

var client = new WafDetectorClient();
var urls = await File.ReadAllLinesAsync("targets.txt");

// Filter out comments and empty lines
urls = urls.Where(u => !string.IsNullOrWhiteSpace(u) && !u.StartsWith("#")).ToArray();

var results = await client.DetectBatchAsync(urls, maxConcurrency: 5);

// Save results
var output = results.Select(r => $"{r.Key},{r.Value.Waf?.Name ?? "None"}");
await File.WriteAllLinesAsync("results.csv", output);
```

## Provider Management

### List Providers

```csharp
using WafSight;

var client = new WafDetectorClient();
var providers = client.ListProviders();

foreach (var provider in providers)
{
    Console.WriteLine($"{provider.Name} (Priority: {provider.Priority})");
}
```

### Register Custom Provider

```csharp
using WafSight;
using WafSight.Providers;

var client = new WafDetectorClient();
client.RegisterProvider(new MyCustomProvider());

var result = await client.DetectAsync("https://example.com");
```

### Provider Count

```csharp
var client = new WafDetectorClient();
Console.WriteLine($"Registered providers: {client.GetProviderCount()}");
```

## Passive Detection (No HTTP Requests)

Analyze an existing HTTP response without making additional network requests.

### Basic Usage

```csharp
using WafSight;
using WafSight.Models;

var client = new WafDetectorClient();

// Build response data from an existing response (e.g. Playwright, HttpClient, proxy)
var response = new HttpResponseData
{
    Url = "https://example.com/admin",
    StatusCode = 403,
    Headers = new Dictionary<string, string>
    {
        { "cf-ray", "abc123-CDG" },
        { "server", "cloudflare" }
    },
    Body = "<html>Access Denied</html>"
};

var result = await client.DetectFromResponseAsync(response);

Console.WriteLine($"WAF detected: {result.HasWaf}");
Console.WriteLine($"WAF name: {result.Waf?.Name ?? "none"}");
Console.WriteLine($"Confidence: {result.Waf?.Confidence:P0}");
```

### From Browser Automation (Playwright)

```csharp
// Example: analyzing a response already captured by Playwright
var playwrightResponse = await page.GotoAsync("https://example.com");
var statusCode = playwrightResponse.Status;
var headers = playwrightResponse.Headers;
var body = await playwrightResponse.BodyAsync();

var response = new HttpResponseData
{
    Url = playwrightResponse.Url,
    StatusCode = (int)statusCode,
    Headers = new Dictionary<string, string>(headers, StringComparer.OrdinalIgnoreCase),
    Body = System.Text.Encoding.UTF8.GetString(body)
};

var result = await client.DetectFromResponseAsync(response);
```

### With Error Handling

```csharp
try
{
    var result = await client.DetectFromResponseAsync(response);
    
    if (result.HasWaf)
    {
        Console.WriteLine($"Protected by {result.Waf.Name} ({result.Waf.Confidence:P0})");
        
        foreach (var evidence in result.Evidence)
        {
            Console.WriteLine($"  [{evidence.Method}] {evidence.Name}: {evidence.Value}");
        }
    }
    
    if (result.HasCdn)
    {
        Console.WriteLine($"CDN: {result.Cdn.Name} ({result.Cdn.Confidence:P0})");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"Passive detection failed: {ex.Message}");
}
```

### Integration with Existing WAF Detectors

```csharp
// Use passive detection as a pre-filter before active probing
var passiveResult = await client.DetectFromResponseAsync(response);

if (passiveResult.HasWaf)
{
    Console.WriteLine("WAF detected passively, skipping active probing");
}
else
{
    // Fall back to active detection (makes HTTP requests)
    var activeResult = await client.DetectAsync(response.Url);
}
```

## Result Analysis

### Check Detection

```csharp
var result = await client.DetectAsync("https://example.com");

if (result.HasWaf)
{
    Console.WriteLine($"WAF: {result.Waf.Name}");
    Console.WriteLine($"Confidence: {result.Waf.Confidence:P0}");
}

if (result.HasCdn)
{
    Console.WriteLine($"CDN: {result.Cdn.Name}");
    Console.WriteLine($"Confidence: {result.Cdn.Confidence:P0}");
}
```

### View Evidence

```csharp
var result = await client.DetectAsync("https://cloudflare.com");

Console.WriteLine($"Evidence count: {result.Evidence.Count}");

foreach (var evidence in result.Evidence)
{
    Console.WriteLine($"[{evidence.Method}] {evidence.Name} = {evidence.Value} ({evidence.Confidence:P0})");
}
```

### Provider Scores

```csharp
var result = await client.DetectAsync("https://example.com");

Console.WriteLine("Provider Scores:");
foreach (var (name, score) in result.ProviderScores.OrderByDescending(s => s.Value))
{
    Console.WriteLine($"  {name}: {score:P0}");
}
```

### Detection Time

```csharp
var result = await client.DetectAsync("https://example.com");
Console.WriteLine($"Detection time: {result.DetectionTimeMs}ms");
```

## ASP.NET Core Integration

### In Controller

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
    public async Task<IActionResult> Detect(string url)
    {
        var result = await _detector.DetectAsync(url);
        return Ok(result);
    }
}
```

### Background Service

```csharp
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
        var urls = new[] { "https://example.com", "https://target.com" };
        
        while (!stoppingToken.IsCancellationRequested)
        {
            var results = await _detector.DetectBatchAsync(urls);
            
            foreach (var (url, result) in results)
            {
                if (result.HasWaf)
                {
                    _logger.LogInformation("{Url} protected by {Waf}", url, result.Waf.Name);
                }
            }
            
            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
        }
    }
}
```

## Disposing Resources

```csharp
// Client implements IDisposable
using (var client = new WafDetectorClient())
{
    var result = await client.DetectAsync("https://example.com");
}

// Or use using statement
await using var client = new WafDetectorClient();
var result = await client.DetectAsync("https://example.com");
```

## Performance Tips

1. **Reuse client instances** - Don't create new for each detection
2. **Control concurrency** - Use `maxConcurrency` in batch operations
3. **Use appropriate timeout** - Default is 10 seconds
4. **Disable unused features** - See DI integration for options

## See Also

- [DI Integration](di-integration.md) - ASP.NET Core dependency injection
- [Custom Providers](custom-providers.md) - Extend detection capabilities
- [API Reference](api-reference.md) - Complete API documentation
