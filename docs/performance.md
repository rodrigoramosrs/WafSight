# Performance Tips

Optimize WafSight detection speed and resource usage.

## Overview

WafSight is designed for performance, but proper configuration can significantly improve detection speed.

## Quick Wins

### 1. Control Concurrency

**Problem:** Too many concurrent requests overwhelm target or cause detection delays.

**Solution:** Limit `maxConcurrency`:

```csharp
// Too many concurrent requests
var results = await client.DetectBatchAsync(urls, maxConcurrency: 50);

// Recommended
var results = await client.DetectBatchAsync(urls, maxConcurrency: 5);
```

**Guidelines:**
- Small batches (< 10 URLs): `maxConcurrency: 3-5`
- Medium batches (10-100): `maxConcurrency: 5-10`
- Large batches (> 100): `maxConcurrency: 10-20`

### 2. Use Appropriate Timeout

**Problem:** Default timeout (10s) may be too long for slow servers.

**Solution:** Set shorter timeout:

```csharp
var client = new WafDetectorClient(timeout: TimeSpan.FromSeconds(5));
```

**Guidelines:**
- Fast servers: `TimeSpan.FromSeconds(5)`
- Slow servers: `TimeSpan.FromSeconds(15)`
- Unreliable networks: `TimeSpan.FromSeconds(30)`

### 3. Disable Unused Features

**Problem:** Generic detection and DNS analysis add overhead.

**Solution:** Disable if not needed:

```csharp
services.AddWafDetector(options =>
{
    options.EnableGenericDetection = false;  // Skip payload probing
    options.EnableDnsAnalysis = false;       // Skip DNS checks
});
```

**Impact:**
- Disable generic detection: -30% time
- Disable DNS analysis: -20% time
- Both disabled: -50% time

## Advanced Optimization

### 1. Reuse Client Instances

**Problem:** Creating new clients for each detection adds overhead.

**Solution:** Reuse client:

```csharp
// Bad: Create new client each time
foreach (var url in urls)
{
    using var client = new WafDetectorClient();
    var result = await client.DetectAsync(url);
}

// Good: Reuse client
using var client = new WafDetectorClient();
foreach (var url in urls)
{
    var result = await client.DetectAsync(url);
}
```

**Impact:** -10% time, less GC pressure

### 2. Batch Processing

**Problem:** Single URL detection is inefficient for multiple URLs.

**Solution:** Use batch detection:

```csharp
// Bad: Single detections
foreach (var url in urls)
{
    var result = await client.DetectAsync(url);
}

// Good: Batch detection
var results = await client.DetectBatchAsync(urls, maxConcurrency: 10);
```

**Impact:** -40% time, better resource utilization

### 3. Selective Provider Detection

**Problem:** Running all providers when you only need specific ones.

**Solution:** Register only needed providers:

```csharp
var client = new WafDetectorClient();

// Only detect CloudFlare and AWS
client.RegisterProvider(new CloudFlareProvider());
client.RegisterProvider(new AwsProvider());

// Don't register others
// client.RegisterProvider(new AkamaiProvider());
```

**Impact:** -50% time for small provider sets

### 4. Caching Results

**Problem:** Re-detecting same URLs wastes resources.

**Solution:** Implement caching:

```csharp
var cache = new Dictionary<string, DetectionResult>();

foreach (var url in urls)
{
    if (cache.TryGetValue(url, out var cached))
    {
        results[url] = cached;
        continue;
    }
    
    var result = await client.DetectAsync(url);
    cache[url] = result;
    results[url] = result;
}
```

**Impact:** -90% time for repeated detections

### 5. Parallel Detection with Limiters

**Problem:** Need fast detection but avoid overwhelming targets.

**Solution:** Use SemaphoreSlim:

```csharp
var semaphore = new SemaphoreSlim(10); // Max 10 concurrent
var tasks = urls.Select(async url =>
{
    await semaphore.WaitAsync();
    try
    {
        return await client.DetectAsync(url);
    }
    finally
    {
        semaphore.Release();
    }
});

var results = await Task.WhenAll(tasks);
```

## CLI Optimization

### Use Appropriate Verbosity

**Problem:** High verbosity slows down CLI.

**Solution:** Use minimal verbosity for scripts:

```bash
# Fast (no logging)
WafSight -V 0 detect <url>

# Slow (full logging)
WafSight -V 3 detect <url>
```

**Impact:** -20% time with `-V 0`

### Batch Mode

**Problem:** Single URL detection in scripts.

**Solution:** Use batch mode:

```bash
# Bad: Single detections
for url in $(cat urls.txt); do
    WafSight detect $url
done

# Good: Batch mode
WafSight batch urls.txt
```

**Impact:** -30% time, less process overhead

## Network Optimization

### 1. Use HTTPS

**Problem:** HTTP requires extra redirects.

**Solution:** Always use HTTPS:

```csharp
var url = "https://example.com";  // Good
var url = "http://example.com";   // Bad (redirects to HTTPS)
```

**Impact:** -10% time

### 2. Disable SSL Verification (Not Recommended)

**Warning:** Only for internal networks!

```csharp
var handler = new HttpClientHandler
{
    ServerCertificateCustomValidationCallback = 
        HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
};
```

**Impact:** -5% time, security risk

### 3. DNS Caching

**Problem:** Repeated DNS lookups slow down detection.

**Solution:** Use OS-level DNS caching or custom cache:

```csharp
// OS-level: Enable DNS cache
// Windows: netsh interface ipv4 set dns "Ethernet" static 8.8.8.8

// Application-level: Custom DNS cache
var dnsCache = new Dictionary<string, DnsInfo>();

foreach (var url in urls)
{
    var host = new Uri(url).Host;
    
    if (dnsCache.TryGetValue(host, out var cachedDns))
    {
        // Use cached DNS
    }
    else
    {
        var dnsInfo = await dnsAnalyzer.ResolveAsync(url);
        dnsCache[host] = dnsInfo;
    }
}
```

## Memory Optimization

### 1. Dispose Resources

**Problem:** Not disposing clients leaks resources.

**Solution:** Use `using` statements:

```csharp
// Good
using var client = new WafDetectorClient();
var result = await client.DetectAsync(url);

// Bad
var client = new WafDetectorClient();
var result = await client.DetectAsync(url);
// Client not disposed
```

### 2. Limit Response Body Size

**Problem:** Large response bodies slow down body analysis.

**Solution:** Truncate body:

```csharp
// In provider
var body = response.Body?.Substring(0, Math.Min(response.Body.Length, 10000)) ?? "";
```

**Impact:** -15% time for large responses

## Monitoring Performance

### Measure Detection Time

```csharp
var stopwatch = Stopwatch.StartNew();
var result = await client.DetectAsync(url);
stopwatch.Stop();

Console.WriteLine($"Detection time: {stopwatch.ElapsedMilliseconds}ms");
```

### Log Performance Metrics

```csharp
services.AddLogging(builder =>
{
    builder.AddConsole();
    builder.SetMinimumLevel(LogLevel.Debug);
});
```

**Log output:**
```
dbug: WafSight.WafDetectorClient[0]
      Detection completed for: example.com, Found: False, Time: 45ms
```

### Profile with Visual Studio

1. **Start debugging** (F5)
2. **Run detection**
3. **Go to Debug → Performance Profiler**
4. **Select "CPU Usage"**
5. **Start profiling**
6. **Run detection**
7. **Stop profiling**
8. **Analyze results**

## Benchmarking

### Simple Benchmark

```csharp
var sw = Stopwatch.StartNew();
var results = await client.DetectBatchAsync(urls, maxConcurrency: 10);
sw.Stop();

var avgTime = sw.ElapsedMilliseconds / urls.Count;
Console.WriteLine($"Average detection time: {avgTime}ms");
```

### Compare Configurations

```csharp
// Configuration 1: Default
var client1 = new WafDetectorClient();
var time1 = await Benchmark(client1, urls);

// Configuration 2: Optimized
var client2 = new WafDetectorClient(timeout: TimeSpan.FromSeconds(5));
var time2 = await Benchmark(client2, urls);

Console.WriteLine($"Default: {time1}ms, Optimized: {time2}ms");
```

## Troubleshooting Performance Issues

### Slow Detection

**Check:**
- Network latency
- Target server response time
- DNS resolution speed
- Provider count (too many?)

**Solution:**
- Reduce `maxConcurrency`
- Shorten timeout
- Disable unused providers

### High Memory Usage

**Check:**
- Client disposal
- Response body size
- Cache size

**Solution:**
- Use `using` statements
- Truncate response bodies
- Implement cache limits

### High CPU Usage

**Check:**
- Payload probing enabled?
- Too many concurrent requests?
- Provider count?

**Solution:**
- Disable generic detection
- Reduce concurrency
- Remove unnecessary providers

## See Also

- [Library Integration](library-integration.md) - Usage examples
- [CLI Reference](cli-reference.md) - CLI options
- [Detection Methods](detection-methods.md) - How detection works
