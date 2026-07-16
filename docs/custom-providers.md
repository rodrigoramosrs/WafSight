# Custom Providers

Learn how to create custom detection providers to extend WafSight's capabilities.

## Overview

WafSight's provider system is fully extensible. You can create custom providers to detect:
- **Proprietary WAFs** not covered by built-in providers
- **CDN services** specific to your infrastructure
- **Custom security products** used in your organization
- **Research purposes** - test new detection methods

## Provider Interface

All providers implement `IDetectionProvider`:

```csharp
public interface IDetectionProvider
{
    string Name { get; }
    string Version { get; }
    string Description { get; }
    ProviderType ProviderType { get; }
    double ConfidenceBase { get; }
    int Priority { get; }
    bool Enabled { get; }
    
    Task<List<Evidence>> DetectAsync(DetectionContext context);
    Task<List<Evidence>> PassiveDetectAsync(HttpResponseData response);
}
```

## Creating a Basic Provider

### Step 1: Create the Class

```csharp
using WafSight;
using WafSight.Models;
using WafSight.Providers;

public class MyCustomWaf : IDetectionProvider
{
    public string Name => "MyCustomWAF";
    public string Version => "1.0.0";
    public string Description => "Custom WAF detection";
    public ProviderType ProviderType => ProviderType.WAF;
    public double ConfidenceBase => 0.85;
    public int Priority => 50;
    public bool Enabled => true;
    
    public Task<List<Evidence>> DetectAsync(DetectionContext context)
    {
        var evidence = new List<Evidence>();
        
        if (context.Response is not null)
        {
            // Check for custom headers
            if (context.Response.Headers.TryGetValue("x-custom-waf", out var value))
            {
                evidence.Add(new Evidence
                {
                    Method = DetectionMethod.Header,
                    Name = "x-custom-waf",
                    Value = value,
                    Confidence = 0.90,
                    Description = "Custom WAF header detected"
                });
            }
        }
        
        return Task.FromResult(evidence);
    }
    
    public Task<List<Evidence>> PassiveDetectAsync(HttpResponseData response)
    {
        return DetectAsync(new DetectionContext { Response = response });
    }
}
```

### Step 2: Register the Provider

```csharp
using WafSight;

var client = new WafDetectorClient();
client.RegisterProvider(new MyCustomWaf());

var result = await client.DetectAsync("https://example.com");
```

## Advanced Detection Methods

### Header Detection

```csharp
public Task<List<Evidence>> DetectAsync(DetectionContext context)
{
    var evidence = new List<Evidence>();
    
    if (context.Response?.Headers is null)
        return Task.FromResult(evidence);
    
    // Multiple headers
    var headers = new[] { "x-waf-1", "x-waf-2", "x-waf-3" };
    
    foreach (var header in headers)
    {
        if (context.Response.Headers.TryGetValue(header, out var value))
        {
            evidence.Add(new Evidence
            {
                Method = DetectionMethod.Header,
                Name = header,
                Value = value,
                Confidence = 0.85,
                Description = $"{header} header detected",
                Signature = $"header-{header}",
                Weight = 0.85
            });
        }
    }
    
    return Task.FromResult(evidence);
}
```

### Body Pattern Detection

```csharp
using System.Text.RegularExpressions;

public Task<List<Evidence>> DetectAsync(DetectionContext context)
{
    var evidence = new List<Evidence>();
    
    if (context.Response?.Body is null)
        return Task.FromResult(evidence);
    
    // Check for specific patterns
    var patterns = new Dictionary<string, Regex>
    {
        { "error-page", new Regex(@"(?i)access\s+denied|forbidden", RegexOptions.Compiled) },
        { "captcha", new Regex(@"(?i)captcha|verify\s+human", RegexOptions.Compiled) }
    };
    
    foreach (var (name, pattern) in patterns)
    {
        if (pattern.IsMatch(context.Response.Body))
        {
            evidence.Add(new Evidence
            {
                Method = DetectionMethod.Body,
                Name = name,
                Value = "pattern matched",
                Confidence = 0.70,
                Description = $"{name} pattern detected in response body",
                Signature = $"body-{name}",
                Weight = 0.50
            });
        }
    }
    
    return Task.FromResult(evidence);
}
```

### Status Code Detection

```csharp
public Task<List<Evidence>> DetectAsync(DetectionContext context)
{
    var evidence = new List<Evidence>();
    
    if (context.Response is null)
        return Task.FromResult(evidence);
    
    // 403 with specific headers
    if (context.Response.StatusCode == 403)
    {
        if (context.Response.Headers.ContainsKey("x-waf-blocked"))
        {
            evidence.Add(new Evidence
            {
                Method = DetectionMethod.StatusCode,
                Name = "403-with-header",
                Value = "403",
                Confidence = 0.80,
                Description = "403 Forbidden with WAF header",
                Signature = "status-403-waf",
                Weight = 0.75
            });
        }
    }
    
    // 429 Rate limiting
    if (context.Response.StatusCode == 429)
    {
        evidence.Add(new Evidence
        {
            Method = DetectionMethod.StatusCode,
            Name = "429-rate-limit",
            Value = "429",
            Confidence = 0.75,
            Description = "Rate limiting detected",
            Signature = "status-429",
            Weight = 0.70
        });
    }
    
    return Task.FromResult(evidence);
}
```

### Cookie Detection

```csharp
public Task<List<Evidence>> DetectAsync(DetectionContext context)
{
    var evidence = new List<Evidence>();
    
    if (context.Response?.Headers is null)
        return Task.FromResult(evidence);
    
    if (context.Response.Headers.TryGetValue("set-cookie", out var cookies))
    {
        // Check for tracking/security cookies
        if (cookies.Contains("__waf_session"))
        {
            evidence.Add(new Evidence
            {
                Method = DetectionMethod.Cookie,
                Name = "waf-session",
                Value = "__waf_session",
                Confidence = 0.85,
                Description = "WAF session cookie detected",
                Signature = "cookie-waf-session",
                Weight = 0.85
            });
        }
    }
    
    return Task.FromResult(evidence);
}
```

### DNS-based Detection

```csharp
using WafSight.Http;

public Task<List<Evidence>> DetectAsync(DetectionContext context)
{
    var evidence = new List<Evidence>();
    
    if (context.DnsInfo is null)
        return Task.FromResult(evidence);
    
    // Check CNAME records
    if (context.DnsInfo.Cnames is not null)
    {
        foreach (var cname in context.DnsInfo.Cnames)
        {
            if (cname.Contains("custom-waf.example.com"))
            {
                evidence.Add(new Evidence
                {
                    Method = DetectionMethod.DNS,
                    Name = "cname-waf",
                    Value = cname,
                    Confidence = 0.80,
                    Description = "WAF CNAME record detected",
                    Signature = "dns-cname-waf",
                    Weight = 0.95
                });
            }
        }
    }
    
    return Task.FromResult(evidence);
}
```

## Provider Metadata

### ProviderType

```csharp
[Flags]
public enum ProviderType
{
    None = 0,
    WAF = 1,
    CDN = 2,
    Both = WAF | CDN
}
```

Choose appropriately:
- `ProviderType.WAF` - Only WAF detection
- `ProviderType.CDN` - Only CDN detection  
- `ProviderType.Both` - Both WAF and CDN

### Priority

Priority determines detection order (higher = checked first):

```csharp
public int Priority => 50;  // 0-100, higher = checked first
```

**Guidelines:**
- Well-known providers: 80-100
- Medium confidence: 50-79
- Custom/low confidence: 0-49

### ConfidenceBase

Base confidence score before evidence is added:

```csharp
public double ConfidenceBase => 0.85;  // 0.0 to 1.0
```

## Complete Example

```csharp
using System.Text.RegularExpressions;
using WafSight;
using WafSight.Models;
using WafSight.Providers;

public class EnterpriseWaf : IDetectionProvider
{
    public string Name => "EnterpriseWAF";
    public string Version => "2.0.0";
    public string Description => "Enterprise WAF detection for internal systems";
    public ProviderType ProviderType => ProviderType.Both;
    public double ConfidenceBase => 0.90;
    public int Priority => 75;
    public bool Enabled => true;

    private static readonly Regex SessionCookiePattern = new(
        @"(?i)enterprise_waf_session=[a-f0-9]+", RegexOptions.Compiled);
    
    private static readonly Regex ErrorResponsePattern = new(
        @"(?i)<title>Access\s+Denied</title>", RegexOptions.Compiled);

    public async Task<List<Evidence>> DetectAsync(DetectionContext context)
    {
        var evidence = new List<Evidence>();

        if (context.Response is not null)
        {
            evidence.AddRange(await CheckHeaders(context.Response));
            evidence.AddRange(await CheckCookies(context.Response));
            evidence.AddRange(await CheckBody(context.Response));
        }

        if (context.DnsInfo is not null)
        {
            evidence.AddRange(CheckDns(context.DnsInfo));
        }

        return evidence;
    }

    private Task<List<Evidence>> CheckHeaders(HttpResponseData response)
    {
        var evidence = new List<Evidence>();
        
        if (response.Headers.TryGetValue("x-enterprise-waf", out var value))
        {
            evidence.Add(new Evidence
            {
                Method = DetectionMethod.Header,
                Name = "x-enterprise-waf",
                Value = value,
                Confidence = 0.95,
                Description = "Enterprise WAF header detected",
                Signature = "header-enterprise-waf",
                Weight = 1.0
            });
        }
        
        return Task.FromResult(evidence);
    }

    private Task<List<Evidence>> CheckCookies(HttpResponseData response)
    {
        var evidence = new List<Evidence>();
        
        if (response.Headers.TryGetValue("set-cookie", out var cookies))
        {
            if (SessionCookiePattern.IsMatch(cookies))
            {
                evidence.Add(new Evidence
                {
                    Method = DetectionMethod.Cookie,
                    Name = "enterprise-session",
                    Value = "detected",
                    Confidence = 0.90,
                    Description = "Enterprise WAF session cookie",
                    Signature = "cookie-enterprise-session",
                    Weight = 0.85
                });
            }
        }
        
        return Task.FromResult(evidence);
    }

    private Task<List<Evidence>> CheckBody(HttpResponseData response)
    {
        var evidence = new List<Evidence>();
        
        if (ErrorResponsePattern.IsMatch(response.Body))
        {
            evidence.Add(new Evidence
            {
                Method = DetectionMethod.Body,
                Name = "access-denied-page",
                Value = "detected",
                Confidence = 0.75,
                Description = "Access denied page detected",
                Signature = "body-access-denied",
                Weight = 0.50
            });
        }
        
        return Task.FromResult(evidence);
    }

    private List<Evidence> CheckDns(DnsInfo dnsInfo)
    {
        var evidence = new List<Evidence>();
        
        if (dnsInfo.Cnames is not null)
        {
            foreach (var cname in dnsInfo.Cnames)
            {
                if (cname.Contains("waf.enterprise.local"))
                {
                    evidence.Add(new Evidence
                    {
                        Method = DetectionMethod.DNS,
                        Name = "cname-waf",
                        Value = cname,
                        Confidence = 0.85,
                        Description = "WAF CNAME in DNS",
                        Signature = "dns-cname-waf",
                        Weight = 0.95
                    });
                }
            }
        }
        
        return evidence;
    }

    public Task<List<Evidence>> PassiveDetectAsync(HttpResponseData response)
    {
        return DetectAsync(new DetectionContext { Response = response });
    }
}
```

## Registering Multiple Providers

```csharp
var client = new WafDetectorClient();

// Add custom providers
client.RegisterProvider(new EnterpriseWaf());
client.RegisterProvider(new AnotherCustomWaf());
client.RegisterProvider(new LegacyWaf());

// Now detects with all providers
var result = await client.DetectAsync("https://example.com");
```

## Testing Your Provider

### Unit Test

```csharp
[Fact]
public async Task DetectAsync_WithHeader_ReturnsEvidence()
{
    var provider = new MyCustomWaf();
    
    var context = new DetectionContext
    {
        Response = new HttpResponseData
        {
            Headers = new Dictionary<string, string>
            {
                { "x-custom-waf", "detected" }
            }
        }
    };
    
    var evidence = await provider.DetectAsync(context);
    
    Assert.Single(evidence);
    Assert.Equal("x-custom-waf", evidence[0].Name);
}
```

### Integration Test

```csharp
[Fact]
public async Task DetectAsync_WithCustomProvider_DetectsCorrectly()
{
    var client = new WafDetectorClient();
    client.RegisterProvider(new MyCustomWaf());
    
    var result = await client.DetectAsync("https://target.com");
    
    Assert.True(result.HasWaf);
    Assert.Equal("MyCustomWAF", result.Waf.Name);
}
```

## Best Practices

1. **Use descriptive names** - `EnterpriseWAF`, not `Waf1`
2. **Set appropriate priority** - Higher for more confident detections
3. **Validate responses** - Always check for null
4. **Use unique signatures** - Avoid collisions between evidence
5. **Test thoroughly** - Write unit tests for each detection method
6. **Document your provider** - Add XML comments explaining detection logic

## See Also

- [Library Integration](library-integration.md) - Using WafSight as a library
- [Provider Architecture](provider-architecture.md) - How providers work internally
- [API Reference](api-reference.md) - Complete API documentation
