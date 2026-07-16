# Provider Architecture

Internal architecture of the WafSight provider system.

## Overview

WafSight uses a pluggable provider architecture for extensible WAF/CDN detection.

```
┌─────────────────────────────────────────┐
│           WafDetectorClient             │
│  (Main entry point, orchestrator)       │
└─────────────────────────────────────────┘
                    │
                    ▼
┌─────────────────────────────────────────┐
│          ProviderRegistry               │
│  (Manages provider registration)        │
└─────────────────────────────────────────┘
                    │
        ┌───────────┼───────────┐
        ▼           ▼           ▼
┌───────────┐ ┌───────────┐ ┌───────────┐
│CloudFlare │ │   AWS     │ │  Akamai   │
│Provider   │ │ Provider  │ │ Provider  │
└───────────┘ └───────────┘ └───────────┘
        │           │           │
        └───────────┼───────────┘
                    ▼
┌─────────────────────────────────────────┐
│      EvidenceScorer                     │
│  (Calculate confidence scores)          │
└─────────────────────────────────────────┘
                    │
                    ▼
┌─────────────────────────────────────────┐
│        DetectionResult                  │
│  (Best WAF, Best CDN, Evidence)         │
└─────────────────────────────────────────┘
```

## Core Components

### 1. IDetectionProvider

Interface all providers must implement.

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

### 2. ProviderRegistry

Manages provider registration and detection orchestration.

```csharp
public class ProviderRegistry
{
    private readonly Dictionary<string, IDetectionProvider> _providers;
    private readonly Dictionary<string, ProviderMetadata> _metadata;
    private readonly EvidenceScorer _scorer;
    
    public void RegisterProvider(IDetectionProvider provider);
    public Task<DetectionResult> DetectAllAsync(DetectionContext context);
    public IReadOnlyList<ProviderMetadata> ListProviders();
}
```

### 3. EvidenceScorer

Calculates confidence scores from evidence.

```csharp
public class EvidenceScorer
{
    private readonly Dictionary<DetectionMethod, double> MethodWeights;
    
    public double CalculateConfidence(List<Evidence> evidence);
    public bool HasTier1Evidence(List<Evidence> evidence);
    public List<string> GenerateCaveats(List<Evidence> evidence, string provider);
}
```

### 4. WafDetectorClient

Main client that orchestrates detection.

```csharp
public class WafDetectorClient : IWafDetector, IDisposable
{
    private readonly ProviderRegistry _registry;
    private readonly WafHttpClient _httpClient;
    private readonly DnsAnalyzer _dnsAnalyzer;
    private readonly GenericDetector _genericDetector;
    
    public Task<DetectionResult> DetectAsync(string url);
    public Task<Dictionary<string, DetectionResult>> DetectBatchAsync(IEnumerable<string> urls);
}
```

## Provider Registration Flow

### 1. Client Initialization

```csharp
var client = new WafDetectorClient();
```

**What happens:**
1. Create `ProviderRegistry`
2. Create `WafHttpClient`
3. Create `DnsAnalyzer`
4. Create `GenericDetector`
5. Register default providers

### 2. Default Providers

```csharp
private void RegisterDefaultProviders()
{
    _registry.RegisterProvider(new CloudFlareProvider(_logger));
    _registry.RegisterProvider(new AwsProvider(_logger));
    _registry.RegisterProvider(new AkamaiProvider(_logger));
    _registry.RegisterProvider(new FastlyProvider(_logger));
    _registry.RegisterProvider(new AzureProvider(_logger));
    _registry.RegisterProvider(new ImpervaProvider(_logger));
    _registry.RegisterProvider(new SucuriProvider(_logger));
    _registry.RegisterProvider(new F5Provider(_logger));
}
```

### 3. Custom Provider Registration

```csharp
client.RegisterProvider(new MyCustomProvider());
```

## Detection Flow

### 1. HTTP Request

```csharp
var response = await _httpClient.GetAsync(url);
```

**Components:**
- `WafHttpClient` - HTTP client with Polly resilience
- Handles timeouts, retries
- Returns `HttpResponseData`

### 2. DNS Resolution

```csharp
var dnsInfo = await _dnsAnalyzer.ResolveAsync(url);
```

**Components:**
- `DnsAnalyzer` - DNS resolution
- Returns `DnsInfo` (CNAMEs, A records, etc.)

### 3. Context Creation

```csharp
var context = new DetectionContext
{
    Url = url,
    Response = response,
    DnsInfo = dnsInfo
};
```

### 4. Provider Detection Loop

```csharp
foreach (var provider in _registry.ListProviders())
{
    if (!provider.Enabled) continue;
    
    var evidence = await provider.DetectAsync(context);
    
    if (evidence.Any())
    {
        var score = _scorer.CalculateConfidence(evidence);
        providerScores[provider.Name] = score;
    }
}
```

### 5. Generic Detection (Fallback)

```csharp
if (!result.Detected && response != null)
{
    var genericResult = await _genericDetector.DetectGenericAsync(
        context,
        async u => await _httpClient.GetAsync(u, cancellationToken));
}
```

**Purpose:** Detect WAFs not covered by specific providers.

### 6. Best Provider Selection

```csharp
var bestWaf = FindBestProvider(providerScores, ProviderType.WAF);
var bestCdn = FindBestProvider(providerScores, ProviderType.CDN);
```

**Logic:**
1. Filter by `ProviderType`
2. Sort by score (descending)
3. Select best with confidence >= 0.60
4. Require Tier 1 evidence

### 7. Result Assembly

```csharp
return new DetectionResult
{
    Url = url,
    Waf = bestWaf,
    Cdn = bestCdn,
    ProviderScores = providerScores,
    Evidence = flatEvidence,
    DetectionTimeMs = detectionTime,
    Caveats = caveats
};
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

### Priority

Priority determines detection order:

```csharp
public int Priority => 100;  // Higher = checked first
```

**Default Priorities:**
- CloudFlare: 100
- AWS: 95
- Akamai: 90
- Fastly: 85
- Azure: 80
- Imperva: 75
- Sucuri: 70
- F5: 65

### ConfidenceBase

Base confidence before evidence:

```csharp
public double ConfidenceBase => 0.95;  // 0.0 to 1.0
```

## Evidence Collection

### Evidence Structure

```csharp
public class Evidence
{
    public DetectionMethod Method { get; set; }
    public string Name { get; set; }
    public string Value { get; set; }
    public double Confidence { get; set; }
    public string Description { get; set; }
    public string Signature { get; set; }
    public double Weight { get; set; }
}
```

### Evidence Methods

| Method | Weight | Description |
|--------|--------|-------------|
| Header | 1.0 | HTTP headers |
| DNS | 0.95 | DNS records |
| Certificate | 0.90 | TLS certificates |
| Cookie | 0.85 | Response cookies |
| StatusCode | 0.75 | HTTP status codes |
| Timing | 0.70 | Response timing |
| Body | 0.50 | Response body patterns |
| Payload | 0.40 | Payload probing |

## Provider Examples

### CloudFlareProvider

**Detection:**
- `cf-ray` header
- `cf-cache-status` header
- `server: cloudflare` header
- `__cfduid` cookie
- 403/429 status codes

**Priority:** 100
**ConfidenceBase:** 0.95

### AwsProvider

**Detection:**
- `x-amz-cf-id` header
- `x-amz-cf-pop` header
- AWS certificate issuer

**Priority:** 95
**ConfidenceBase:** 0.90

### GenericDetector

**Detection:**
- Send malicious payloads
- Compare responses
- Detect WAF filtering

**Priority:** N/A (fallback only)
**ConfidenceBase:** 0.60

## Extending the System

### Custom Provider

```csharp
public class MyProvider : IDetectionProvider
{
    public string Name => "MyProvider";
    public ProviderType ProviderType => ProviderType.WAF;
    public double ConfidenceBase => 0.85;
    public int Priority => 50;
    
    public Task<List<Evidence>> DetectAsync(DetectionContext context)
    {
        // Detection logic
    }
}
```

### Register Provider

```csharp
var client = new WafDetectorClient();
client.RegisterProvider(new MyProvider());
```

## Thread Safety

### ProviderRegistry

**Thread-safe:** Yes (using `ConcurrentDictionary`)

**Safe for:**
- Concurrent `RegisterProvider` calls
- Concurrent `DetectAllAsync` calls

### EvidenceScorer

**Thread-safe:** Yes (stateless)

**Safe for:**
- Concurrent `CalculateConfidence` calls

## Performance Considerations

### Provider Count

**Recommendation:** Keep under 20 providers

**Reason:** Each provider runs detection on every request

### Detection Time

**Typical:** 50-200ms per URL

**Bottlenecks:**
- HTTP requests (network)
- DNS resolution
- Payload probing (if enabled)

### Caching

**Not implemented:** Providers don't cache results

**Reason:** URLs may change, cache invalidation complex

## See Also

- [Custom Providers](custom-providers.md) - Create custom providers
- [Detection Methods](detection-methods.md) - How detection works
- [Library Integration](library-integration.md) - Using the library
