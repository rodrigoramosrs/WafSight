# Quick Start Guide

This guide will walk you through your first steps with WafSight, from installation to running your first detection.

## Prerequisites

- .NET 10.0 SDK installed
- Basic knowledge of C# or command-line usage

## Part 1: Using the CLI (Easiest)

### Step 1: Install the CLI

```bash
dotnet tool install --global WafSight
```

### Step 2: Run Your First Detection

```bash
WafSight detect https://example.com
```

**Output:**
```
URL: example.com
WAF: Not detected (N/A)
Evidence: 0 | Time: 45ms
```

### Step 3: Try with Verbose Output

```bash
WafSight -V 3 detect https://cloudflare.com
```

This shows detailed logs including:
- HTTP headers detected
- DNS records
- Provider scores
- Evidence details

### Step 4: List Available Providers

```bash
WafSight providers
```

**Output:**
```
Registered Providers:

Name            Type      Priority  Description
--------------------------------------------------------------------------------
CloudFlare      3         100       CloudFlare WAF/CDN detection provider
AWS             3         95        AWS WAF and CloudFront CDN detection provider
...

Total: 8 providers
```

### Step 5: Batch Detection

Create a file `urls.txt`:
```
https://example.com
https://cloudflare.com
https://aws.amazon.com
```

Run batch detection:
```bash
WafSight batch urls.txt
```

**Output:**
```
URL                                       WAF           CDN           Time
------------------------------------------------------------------------------------------
https://example.com                       -             -             45ms
https://cloudflare.com                    CloudFlare    CloudFlare    52ms
https://aws.amazon.com                    AWS           AWS           48ms
```

## Part 2: Using the Library (DLL)

### Step 1: Create a New Project

```bash
dotnet new console -n MyDetector
cd MyDetector
dotnet add package WafSight
```

### Step 2: Write Detection Code

Replace `Program.cs` with:

```csharp
using WafSight;

var client = new WafDetectorClient();

var result = await client.DetectAsync("https://example.com");

Console.WriteLine($"URL: {result.Url}");
Console.WriteLine($"WAF: {result.Waf?.Name ?? "Not detected"}");
Console.WriteLine($"CDN: {result.Cdn?.Name ?? "Not detected"}");
Console.WriteLine($"Time: {result.DetectionTimeMs}ms");
```

### Step 3: Run

```bash
dotnet run
```

**Output:**
```
URL: https://example.com
WAF: Not detected
CDN: Not detected
Time: 45ms
```

## Part 3: Advanced Usage

### With Dependency Injection

```csharp
using Microsoft.Extensions.DependencyInjection;
using WafSight;

var services = new ServiceCollection();
services.AddWafDetector();
var provider = services.BuildServiceProvider();

var detector = provider.GetRequiredService<IWafDetector>();
var result = await detector.DetectAsync("https://example.com");
```

### With Custom Logging

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

### Batch Detection with Concurrency

```csharp
var urls = File.ReadAllLines("urls.txt");
var results = await client.DetectBatchAsync(urls, maxConcurrency: 10);

foreach (var (url, result) in results)
{
    Console.WriteLine($"{url}: {result.Waf?.Name ?? "None"}");
}
```

## Next Steps

- [CLI Reference](cli-reference.md) - Complete CLI documentation
- [Library Integration](library-integration.md) - Advanced library usage
- [Custom Providers](custom-providers.md) - Create your own detectors


## Common Use Cases

### Security Auditing
```bash
WafSight batch target-list.txt --verbose 2
```

### Monitoring
```csharp
var results = await client.DetectBatchAsync(urls, maxConcurrency: 5);
```

### Integration with Other Tools
```csharp
var result = await client.DetectAsync(url);
if (result.HasWaf)
{
    // Trigger alert, log, etc.
}
```

## Tips

1. **Start with CLI** - Test detections before coding
2. **Use verbosity 2-3** - See what's being detected
3. **Batch mode** - Efficient for multiple URLs
4. **Custom providers** - Extend for proprietary WAFs

## Troubleshooting

**Issue**: Package not found
```bash
dotnet add package WafSight
```
Note: Package name is `WafSight`, not `WafSight`

**Issue**: CLI not recognized
```bash
dotnet tool install --global WafSight
```

**Issue**: Slow detections
- Use `maxConcurrency: 3-5` for batch operations
- Check network connectivity
- Some providers require specific headers

## See Also

- [Installation](installation.md) - Detailed installation options
- [CLI Verbosity](cli-verbosity.md) - Understanding log levels
- [API Reference](api-reference.md) - Complete API documentation
