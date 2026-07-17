# API Reference

Complete API documentation for the WafSight library.

## Table of Contents

- [WafDetectorClient](#wafdetectormanager)
- [IWafDetector](#iwafdector)
- [DetectionResult](#detectionresult)
- [Evidence](#evidence)
- [ProviderMetadata](#providermetadata)
- [DetectionContext](#detectioncontext)
- [ProviderType](#providertype)
- [DetectionMethod](#detectionmethod)
- [WafDetectorOptions](#wafdectoroptions)

---

## WafDetectorClient

Main client for WAF/CDN detection.

### Constructor

```csharp
public WafDetectorClient(
    ILoggerFactory? loggerFactory = null,
    TimeSpan? timeout = null)
```

**Parameters:**
- `loggerFactory` - Optional ILoggerFactory for logging
- `timeout` - HTTP request timeout (default: 10 seconds)

**Example:**
```csharp
var client = new WafDetectorClient(
    loggerFactory: myLoggerFactory,
    timeout: TimeSpan.FromSeconds(30)
);
```

### Methods

#### DetectAsync

Detects WAF/CDN for a single URL.

```csharp
public Task<DetectionResult> DetectAsync(
    string url, 
    CancellationToken cancellationToken = default)
```

**Parameters:**
- `url` - Target URL to detect
- `cancellationToken` - Optional cancellation token

**Returns:** `Task<DetectionResult>`

**Example:**
```csharp
var result = await client.DetectAsync("https://example.com");

if (result.HasWaf)
{
    Console.WriteLine($"WAF: {result.Waf.Name}");
}
```

---

#### DetectFromResponseAsync

Passive detection from an existing HTTP response.
No additional HTTP requests are made — only analyzes headers, body, cookies, and status code.

```csharp
public Task<DetectionResult> DetectFromResponseAsync(
    HttpResponseData response,
    CancellationToken cancellationToken = default)
```

**Parameters:**
- `response` - The HTTP response data (headers, body, status code, URL)
- `cancellationToken` - Optional cancellation token

**Returns:** `Task<DetectionResult>`

**Example:**
```csharp
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

if (result.HasWaf)
{
    Console.WriteLine($"WAF: {result.Waf.Name} ({result.Waf.Confidence:P0})");
}
```

---

#### DetectBatchAsync

Detects WAF/CDN for multiple URLs concurrently.

```csharp
public Task<Dictionary<string, DetectionResult>> DetectBatchAsync(
    IEnumerable<string> urls,
    int maxConcurrency = 3,
    CancellationToken cancellationToken = default)
```

**Parameters:**
- `urls` - Collection of URLs to detect
- `maxConcurrency` - Maximum concurrent requests (default: 3)
- `cancellationToken` - Optional cancellation token

**Returns:** `Task<Dictionary<string, DetectionResult>>`

**Example:**
```csharp
var urls = new[] { "https://example.com", "https://target.com" };
var results = await client.DetectBatchAsync(urls, maxConcurrency: 5);

foreach (var (url, result) in results)
{
    Console.WriteLine($"{url}: {result.Waf?.Name ?? "None"}");
}
```

---

#### ListProviders

Lists all registered detection providers.

```csharp
public IReadOnlyList<ProviderMetadata> ListProviders()
```

**Returns:** `IReadOnlyList<ProviderMetadata>`

**Example:**
```csharp
var providers = client.ListProviders();

foreach (var provider in providers)
{
    Console.WriteLine($"{provider.Name} (Priority: {provider.Priority})");
}
```

---

#### GetProviderCount

Gets the number of registered providers.

```csharp
public int GetProviderCount()
```

**Returns:** `int`

**Example:**
```csharp
Console.WriteLine($"Providers: {client.GetProviderCount()}");
```

---

#### RegisterProvider

Registers a custom detection provider.

```csharp
public void RegisterProvider(IDetectionProvider provider)
```

**Parameters:**
- `provider` - Provider instance to register

**Example:**
```csharp
client.RegisterProvider(new MyCustomProvider());
```

---

## IWafDetector

Interface for WAF/CDN detection clients.

```csharp
public interface IWafDetector
{
    Task<DetectionResult> DetectAsync(string url, CancellationToken cancellationToken = default);
    Task<Dictionary<string, DetectionResult>> DetectBatchAsync(IEnumerable<string> urls, int maxConcurrency = 3, CancellationToken cancellationToken = default);
    IReadOnlyList<ProviderMetadata> ListProviders();
    int GetProviderCount();
    void RegisterProvider(IDetectionProvider provider);
}
```

---

## DetectionResult

Contains detection results for a URL.

### Properties

| Property | Type | Description |
|----------|------|-------------|
| `Url` | `string` | Target URL |
| `Waf` | `ProviderDetection?` | Detected WAF (null if none) |
| `Cdn` | `ProviderDetection?` | Detected CDN (null if none) |
| `HasWaf` | `bool` | True if WAF detected |
| `HasCdn` | `bool` | True if CDN detected |
| `ProviderScores` | `Dictionary<string, double>` | Scores per provider |
| `Evidence` | `List<Evidence>` | All evidence collected |
| `DetectionTimeMs` | `long` | Detection time in milliseconds |
| `Caveats` | `List<string>` | Warnings or caveats |

### Methods

#### ToSummary

Generates a human-readable summary.

```csharp
public string ToSummary()
```

**Returns:** Formatted summary string

**Example:**
```csharp
Console.WriteLine(result.ToSummary());
// Output: "URL: example.com | WAF: CloudFlare | CDN: CloudFlare | Time: 45ms"
```

---

## Evidence

Represents a single piece of detection evidence.

### Properties

| Property | Type | Description |
|----------|------|-------------|
| `Method` | `DetectionMethod` | Detection method used |
| `Name` | `string` | Evidence identifier |
| `Value` | `string` | Evidence value |
| `Confidence` | `double` | Confidence score (0-1) |
| `Description` | `string` | Human-readable description |
| `Signature` | `string` | Unique signature |
| `Weight` | `double` | Weight for scoring |

**Example:**
```csharp
var evidence = new Evidence
{
    Method = DetectionMethod.Header,
    Name = "cf-ray",
    Value = "7890-ABC-123",
    Confidence = 0.95,
    Description = "CloudFlare Ray ID header",
    Signature = "cf-ray-header",
    Weight = 1.0
};
```

---

## ProviderMetadata

Metadata about a detection provider.

### Properties

| Property | Type | Description |
|----------|------|-------------|
| `Name` | `string` | Provider name |
| `Version` | `string` | Provider version |
| `Description` | `string` | Provider description |
| `ProviderType` | `ProviderType` | WAF, CDN, or Both |
| `Enabled` | `bool` | Whether provider is enabled |
| `Priority` | `int` | Detection priority (0-100) |

---

## ProviderDetection

Contains detection result for a specific provider.

### Properties

| Property | Type | Description |
|----------|------|-------------|
| `Name` | `string` | Provider name |
| `Confidence` | `double` | Confidence score (0-1) |

---

## DetectionContext

Context for detection operations.

### Properties

| Property | Type | Description |
|----------|------|-------------|
| `Url` | `string` | Target URL |
| `Response` | `HttpResponseData?` | HTTP response data |
| `DnsInfo` | `DnsInfo?` | DNS resolution information |

---

## HttpResponseData

HTTP response data.

### Properties

| Property | Type | Description |
|----------|------|-------------|
| `StatusCode` | `int` | HTTP status code |
| `Headers` | `Dictionary<string, string>` | Response headers |
| `Body` | `string` | Response body |
| `Url` | `string` | Request URL |
| `ResponseTime` | `TimeSpan` | Response time |

---

## DnsInfo

DNS resolution information.

### Properties

| Property | Type | Description |
|----------|------|-------------|
| `Cnames` | `List<string>?` | CNAME records |
| `ARecords` | `List<string>?` | A records |
| `NsRecords` | `List<string>?` | NS records |
| `TxtRecords` | `List<string>?` | TXT records |

---

## ProviderType

Flags enum for provider types.

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

---

## DetectionMethod

Detection methods used by providers.

```csharp
public enum DetectionMethod
{
    Header = 0,
    DNS = 1,
    Certificate = 2,
    Cookie = 3,
    StatusCode = 4,
    Timing = 5,
    Body = 6,
    Payload = 7
}
```

---

## WafDetectorOptions

Configuration options for WafDetectorClient.

### Properties

| Property | Type | Description |
|----------|------|-------------|
| `Timeout` | `TimeSpan` | HTTP timeout (default: 10s) |
| `UserAgent` | `string?` | Custom User-Agent |
| `EnableGenericDetection` | `bool` | Enable payload probing (default: true) |
| `EnableDnsAnalysis` | `bool` | Enable DNS analysis (default: true) |

**Example:**
```csharp
services.AddWafDetector(options =>
{
    options.Timeout = TimeSpan.FromSeconds(30);
    options.EnableGenericDetection = true;
    options.EnableDnsAnalysis = true;
});
```

---

## IDetectionProvider

Interface for custom detection providers.

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

See [Custom Providers](custom-providers.md) for implementation details.

---

## See Also

- [Library Integration](library-integration.md) - Usage examples
- [DI Integration](di-integration.md) - Dependency injection
- [Custom Providers](custom-providers.md) - Create custom providers
