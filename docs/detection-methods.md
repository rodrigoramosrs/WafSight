# Detection Methods

Internal details of how WafSight detects WAFs and CDNs.

## Overview

WafSight uses multiple detection methods to identify WAFs and CDNs:

1. **HTTP Headers** (passive) - Analyze response headers
2. **DNS Records** (active) - Check DNS configuration
3. **TLS Certificates** (active) - Examine certificate details
4. **Cookies** (passive) - Look for tracking/security cookies
5. **Status Codes** (passive) - Analyze HTTP status codes
6. **Response Timing** (active) - Measure response times
7. **Response Body** (passive) - Scan for patterns
8. **Payload Probing** (active) - Send test payloads

> **Passive methods** (headers, cookies, status codes, body) can be used via `DetectFromResponseAsync(HttpResponseData)` without making any additional HTTP requests. Active methods (DNS, TLS, timing, payload probing) require the full `DetectAsync(url)` which performs network requests.

## Detection Flow

```
┌─────────────────────────────────────┐
│         DetectionContext            │
│  (URL, HTTP Response, DNS Info)     │
└─────────────────────────────────────┘
              │
              ▼
┌─────────────────────────────────────┐
│      Provider Detection Loop        │
│  (CloudFlare, AWS, Akamai, etc.)    │
└─────────────────────────────────────┘
              │
              ▼
┌─────────────────────────────────────┐
│       Evidence Collection           │
│  (Headers, DNS, Cookies, etc.)      │
└─────────────────────────────────────┘
              │
              ▼
┌─────────────────────────────────────┐
│      EvidenceScorer                 │
│  (Calculate confidence scores)      │
└─────────────────────────────────────┘
              │
              ▼
┌─────────────────────────────────────┐
│      DetectionResult                │
│  (Best WAF, Best CDN, Evidence)     │
└─────────────────────────────────────┘
```

## 1. HTTP Headers

### Method Weight: 1.0 (Highest)

Headers are the most reliable detection method.

### Detection Process

1. **Collect response headers**
2. **Match against known patterns**
3. **Assign confidence scores**

### Examples

**CloudFlare:**
```
cf-ray: 7890-ABC-123
cf-cache-status: HIT
server: cloudflare
```

**AWS CloudFront:**
```
x-amz-cf-id: abc123
x-amz-cf-pop: GRU50-P1
```

**Akamai:**
```
x-ac-transform: akgslb
x-ak-iq-timings: ...
```

### Implementation

```csharp
if (response.Headers.TryGetValue("cf-ray", out var cfRay))
{
    if (CfRayPattern.IsMatch(cfRay))
    {
        evidence.Add(new Evidence
        {
            Method = DetectionMethod.Header,
            Name = "cf-ray",
            Value = cfRay,
            Confidence = 0.95,
            Description = "CloudFlare Ray ID header",
            Signature = "cf-ray-header",
            Weight = 1.0
        });
    }
}
```

## 2. DNS Records

### Method Weight: 0.95

DNS analysis reveals CDN infrastructure.

### Detection Process

1. **Resolve DNS records**
2. **Check CNAME aliases**
3. **Analyze A records**
4. **Look for specific patterns**

### Examples

**CloudFlare:**
```
CNAME: a.nic.cloudflare.com
A: 104.16.0.1, 104.16.1.1
```

**AWS CloudFront:**
```
CNAME: d1234.cloudfront.net
```

**Akamai:**
```
CNAME: a1234.akamai.net
```

### Implementation

```csharp
var dnsInfo = await dnsAnalyzer.ResolveAsync(url);

if (dnsInfo?.Cnames is not null)
{
    foreach (var cname in dnsInfo.Cnames)
    {
        if (cname.Contains("cloudflare.com"))
        {
            evidence.Add(new Evidence
            {
                Method = DetectionMethod.DNS,
                Name = "cname-cloudflare",
                Value = cname,
                Confidence = 0.90,
                Description = "CloudFlare CNAME",
                Signature = "dns-cname-cf",
                Weight = 0.95
            });
        }
    }
}
```

## 3. TLS Certificates

### Method Weight: 0.90

Certificate analysis can reveal CDN providers.

### Detection Process

1. **Connect to target**
2. **Extract certificate**
3. **Check issuer**
4. **Analyze SANs**

### Examples

**CloudFlare:**
```
Issuer: CloudFlare Inc. ECC Certification Authority
SANs: *.example.com, example.com
```

**AWS:**
```
Issuer: Amazon
SANs: *.cloudfront.net
```

### Implementation

```csharp
using var handler = new HttpClientHandler();
using var client = new HttpClient(handler);

var request = new HttpRequestMessage(HttpMethod.Head, url);
using var response = await client.SendAsync(request);

var certificate = handler.SslCertificate;
if (certificate is not null)
{
    if (certificate.Issuer.Contains("CloudFlare"))
    {
        evidence.Add(new Evidence
        {
            Method = DetectionMethod.Certificate,
            Name = "issuer",
            Value = certificate.Issuer,
            Confidence = 0.85,
            Description = "CloudFlare certificate",
            Signature = "cert-issuer-cf",
            Weight = 0.90
        });
    }
}
```

## 4. Cookies

### Method Weight: 0.85

Security cookies indicate WAF/CDN protection.

### Detection Process

1. **Parse Set-Cookie headers**
2. **Match against known patterns**
3. **Assign confidence**

### Examples

**CloudFlare:**
```
__cfduid=d1234567890abcdef; ...
__cf_bm=abc123; ...
```

**Imperva:**
```
incap_ses=abc123; ...
visid_incap=def456; ...
```

### Implementation

```csharp
if (response.Headers.TryGetValue("set-cookie", out var cookies))
{
    if (cookies.Contains("__cfduid"))
    {
        evidence.Add(new Evidence
        {
            Method = DetectionMethod.Cookie,
            Name = "__cfduid",
            Value = "detected",
            Confidence = 0.85,
            Description = "CloudFlare cookie",
            Signature = "cookie-cfduid",
            Weight = 0.85
        });
    }
}
```

## 5. Status Codes

### Method Weight: 0.75

HTTP status codes can indicate WAF blocks.

### Detection Process

1. **Check status code**
2. **Analyze response body**
3. **Look for WAF indicators**

### Examples

**403 Forbidden:**
```http
HTTP/1.1 403 Forbidden
Server: cloudflare
```

**429 Too Many Requests:**
```http
HTTP/1.1 429 Too Many Requests
Server: cloudflare
```

### Implementation

```csharp
if (response.StatusCode == 403)
{
    if (response.Headers.ContainsKey("cf-ray"))
    {
        evidence.Add(new Evidence
        {
            Method = DetectionMethod.StatusCode,
            Name = "403-with-cf",
            Value = "403",
            Confidence = 0.75,
            Description = "CloudFlare 403",
            Signature = "status-403-cf",
            Weight = 0.75
        });
    }
}
```

## 6. Response Timing

### Method Weight: 0.70

CDNs often add measurable latency.

### Detection Process

1. **Measure response time**
2. **Compare to baseline**
3. **Analyze patterns**

### Implementation

```csharp
var startTime = Stopwatch.StartNew();
var response = await httpClient.GetAsync(url);
startTime.Stop();

var responseTime = startTime.ElapsedMilliseconds;

if (responseTime > 100 && responseTime < 500)
{
    // Potential CDN caching
    evidence.Add(new Evidence
    {
        Method = DetectionMethod.Timing,
        Name = "response-time",
        Value = $"{responseTime}ms",
        Confidence = 0.60,
        Description = "Response time suggests CDN",
        Signature = "timing-cdn",
        Weight = 0.70
    });
}
```

## 7. Response Body

### Method Weight: 0.50

Body patterns can indicate WAF challenge pages.

### Detection Process

1. **Download response body**
2. **Search for patterns**
3. **Match against known signatures**

### Examples

**CloudFlare Challenge:**
```html
<title>Checking your browser before accessing...</title>
<form id="cf-chl-render...">
```

**Imperva:**
```html
<center><h1>403 Forbidden</h1></center>
```

### Implementation

```csharp
var body = response.Body?.ToLower() ?? "";

if (CfChallengePattern.IsMatch(body))
{
    evidence.Add(new Evidence
    {
        Method = DetectionMethod.Body,
        Name = "challenge-page",
        Value = "detected",
        Confidence = 0.70,
        Description = "CloudFlare challenge page",
        Signature = "body-cf-challenge",
        Weight = 0.50
    });
}
```

## 8. Payload Probing (Generic Detection)

### Method Weight: 0.40

Send test payloads to detect WAFs.

### Detection Process

1. **Send malicious payloads**
2. **Compare responses**
3. **Detect WAF filtering**

### Payloads Used

- **XSS:** `<script>alert(1)</script>`
- **SQLi:** `' OR 1=1 --`
- **LFI:** `../../etc/passwd`
- **XXE:** `<!DOCTYPE foo [<!ENTITY xxe SYSTEM "file:///etc/passwd">]>`
- **RCE:** `; cat /etc/passwd`

### Implementation

```csharp
var payloads = new[]
{
    "<script>alert(1)</script>",
    "' OR 1=1 --",
    "../../etc/passwd"
};

foreach (var payload in payloads)
{
    var response = await httpClient.GetAsync(
        url + "?q=" + Uri.EscapeDataString(payload));
    
    if (response.StatusCode == 403 || 
        response.Body.Contains("blocked"))
    {
        evidence.Add(new Evidence
        {
            Method = DetectionMethod.Payload,
            Name = "waf-blocked",
            Value = payload,
            Confidence = 0.65,
            Description = "WAF blocked payload",
            Signature = "payload-waf-block",
            Weight = 0.40
        });
    }
}
```

## Scoring Algorithm

### Evidence Scoring

Each evidence has:
- **Confidence**: 0.0 to 1.0
- **Weight**: Method-specific weight
- **Tier**: Tier 1 (high) or Tier 2 (low)

### Calculation

```csharp
var weightedScore = evidence.Confidence * evidence.Weight;

if (evidence.Tier == Tier1)
{
    weightedScore *= 1.5;  // Tier 1 bonus
}

totalScore += weightedScore;
```

### Confidence Tiers

**Tier 1 (High Confidence):**
- Multiple matching headers
- Specific cookie patterns
- Known WAF signatures

**Tier 2 (Lower Confidence):**
- Single header match
- Generic patterns
- Timing analysis

## Provider Scoring

### Per-Provider Score

```csharp
var providerScore = evidenceScorer.CalculateConfidence(evidence);
```

### Best Provider Selection

```csharp
var bestWaf = providers
    .Where(p => p.Type.HasFlag(ProviderType.WAF))
    .OrderByDescending(p => p.Score)
    .First();
```

## Passive vs Active Detection

Passive detection analyzes an existing HTTP response without making any network requests.
Only methods that rely on response headers, body, cookies, and status codes are used.

| Method | Passive | Active | Requires |
|--------|---------|--------|----------|
| HTTP Headers | ✅ Yes | ✅ Yes | Response headers |
| Cookies | ✅ Yes | ✅ Yes | Set-Cookie headers |
| Status Codes | ✅ Yes | ✅ Yes | Status code |
| Response Body | ✅ Yes | ✅ Yes | Response body |
| DNS Records | ❌ No | ✅ Yes | DNS resolution |
| TLS Certificates | ❌ No | ✅ Yes | TLS connection |
| Response Timing | ❌ No | ✅ Yes | Timing measurement |
| Payload Probing | ❌ No | ✅ Yes | Test payloads |

**When to use passive detection:**
- You already have the HTTP response (Playwright, proxy, curl)
- You want to avoid additional network requests
- You're working in a browser automation context
- Rate limiting or WAF blocking makes active probing risky

## Caveats

### Generate Caveats

```csharp
var caveats = evidenceScorer.GenerateCaveats(evidenceList);
```

**Examples:**
- "Body-only detection (no headers)"
- "Low confidence score"
- "Single evidence point"

## See Also

- [Provider Architecture](provider-architecture.md) - Provider system
- [Custom Providers](custom-providers.md) - Create custom detectors
- [API Reference](api-reference.md) - DetectionResult, Evidence
