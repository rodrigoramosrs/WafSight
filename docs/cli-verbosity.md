# CLI Verbosity Levels

Understanding and using verbosity levels in WafSight CLI for effective debugging and monitoring.

## Overview

Verbosity levels control how much detail WafSight outputs during detection. This is crucial for:

> **Note:** The verbose flag is case-insensitive. `-v`, `-V`, `--verbose`, `--VERBOSE`, and `--Verbose` all work. When used without a value (e.g., `WafSight -v detect <url>`), it defaults to level 3 (High).
- **Debugging** detection issues
- **Monitoring** in production
- **Auditing** security scans
- **Integrating** with other tools

## Verbosity Levels

### Level 0: None (Default)

**Command:**
```bash
WafSight -V 0 detect <url>
WafSight detect <url>  # Same as -V 0
```

**Output:**
```
URL: example.com
WAF: Not detected (N/A)
Evidence: 0 | Time: 45ms
```

**Use Cases:**
- Production scripts
- CI/CD pipelines
- Integration with other tools
- When you only need results

**Log Level:** `LogLevel.None`

---

### Level 1: Low

**Command:**
```bash
WafSight -V 1 detect <url>
```

**Output:**
```
info: WafSight.Program[0]
      WafSight CLI v2026.7.0.1
info: WafSight.WafDetectorClient[0]
      Starting detection for: example.com
info: WafSight.WafDetectorClient[0]
      Detection completed for: example.com, Found: False
URL: example.com
WAF: Not detected (N/A)
Evidence: 0 | Time: 45ms
```

**Shows:**
- CLI version
- Detection start/completion
- Basic results

**Use Cases:**
- Basic monitoring
- Script logging
- When you need to track detection flow

**Log Level:** `LogLevel.Warning`

---

### Level 2: Medium

**Command:**
```bash
WafSight -V 2 detect <url>
```

**Output:**
```
info: WafSight.Program[0]
      WafSight CLI v2026.7.0.1
info: WafSight.WafDetectorClient[0]
      Starting detection for: cloudflare.com
info: WafSight.Http.WafHttpClient[0]
      HTTP request completed for cloudflare.com, Status: 200
info: WafSight.Http.DnsAnalyzer[0]
      DNS resolved for cloudflare.com: A=4, CNAME=2
info: WafSight.Providers.CloudFlareProvider[0]
      CloudFlare cf-ray header detected: 7890-ABC-123
info: WafSight.Providers.CloudFlareProvider[0]
      CloudFlare cf-cache-status detected: HIT
info: WafSight.WafDetectorClient[0]
      Detection completed for: cloudflare.com, WAF=CloudFlare, CDN=CloudFlare, Time=52ms
URL: cloudflare.com
WAF: CloudFlare (95%)
CDN: CloudFlare (95%)
Evidence: 2 | Time: 52ms

Provider Scores:
  CloudFlare          95%
```

**Shows:**
- All Low level information
- HTTP status codes
- DNS resolution details
- Headers detected
- Provider scores
- Detection timing

**Use Cases:**
- Debugging detection issues
- Understanding what headers are detected
- Auditing security posture
- Performance analysis

**Log Level:** `LogLevel.Information`

---

### Level 3: High

**Command:**
```bash
WafSight -V 3 detect <url>
```

**Output:**
```
info: WafSight.Program[0]
      WafSight CLI v2026.7.0.1
dbug: WafSight.WafDetectorClient[0]
      Starting detection for: cloudflare.com
dbug: WafSight.Http.WafHttpClient[0]
      HTTP request started for: cloudflare.com
info: WafSight.Http.WafHttpClient[0]
      HTTP request completed for cloudflare.com, Status: 200, Elapsed: 45ms
dbug: WafSight.Http.DnsAnalyzer[0]
      DNS resolution started for: cloudflare.com
dbug: WafSight.Http.DnsAnalyzer[0]
      A records found: 104.16.0.1, 104.16.1.1, 104.16.2.1, 104.16.3.1
dbug: WafSight.Http.DnsAnalyzer[0]
      CNAME aliases found: a.nic.cloudflare.com
info: WafSight.Providers.CloudFlareProvider[0]
      CloudFlare cf-ray header detected: 7890-ABC-123
info: WafSight.Providers.CloudFlareProvider[0]
      CloudFlare cf-cache-status detected: HIT
dbug: WafSight.Registry.ProviderRegistry[0]
      Running detection for provider: CloudFlare
dbug: WafSight.Registry.ProviderRegistry[0]
      Provider 'CloudFlare' scored 0.950 with 2 evidence(s)
info: WafSight.Analysis.EvidenceScorer[0]
      Scoring completed. Confidence: 0.95, Tier 1 evidence: 2
info: WafSight.Analysis.GenericDetector[0]
      Running generic detection for: cloudflare.com
info: WafSight.WafDetectorClient[0]
      Detection completed for: cloudflare.com, WAF=CloudFlare, CDN=CloudFlare, Time=52ms
URL: cloudflare.com
WAF: CloudFlare (95%)
CDN: CloudFlare (95%)
Evidence: 2 | Time: 52ms

Provider Scores:
  CloudFlare          95%

Evidence:
  [Header] cf-ray = 7890-ABC-123 (95%)
  [Header] cf-cache-status = HIT (90%)
```

**Shows:**
- All Medium level information
- Detailed debug logs
- Payload probing results
- Individual evidence items with confidence
- Scoring details
- Generic detection flow
- Provider registry operations

**Use Cases:**
- Development & testing
- Understanding detection algorithms
- Debugging false positives/negatives
- Contributing to WafSight
- Security research

**Log Level:** `LogLevel.Debug`

---

## Choosing the Right Level

| Scenario | Recommended Level | Reason |
|----------|------------------|--------|
| Production monitoring | `0` | Clean output, minimal overhead |
| CI/CD pipelines | `0` or `1` | Script-friendly output |
| Security auditing | `2` | See what's being detected |
| Debugging issues | `3` | Full diagnostic information |
| Developing providers | `3` | Understand detection flow |
| Integration testing | `2` | Verify detection accuracy |

---

## Logging Framework

WafSight uses [Microsoft.Extensions.Logging](https://learn.microsoft.com/en-us/dotnet/core/extensions/logging), which means you can:

- **Redirect logs** to files
- **Filter by category**
- **Customize output format**
- **Integrate with Serilog, NLog, etc.**

### Example: File Logging

```csharp
var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddFile("logs/wafsight-{Date}.log");
    builder.SetMinimumLevel(LogLevel.Information);
});

var client = new WafDetectorClient(loggerFactory);
```

### Example: Filter by Category

```csharp
builder.AddFilter("WafSight.Providers.CloudFlareProvider", LogLevel.Warning);
builder.AddFilter("WafSight.Http", LogLevel.Information);
```

---

## Tips

1. **Start with `-V 2`** - Good balance of detail
2. **Use `-V 0` in scripts** - Clean, parseable output
3. **Use `-V 3` for debugging** - Full diagnostic info
4. **Combine with grep** - Filter specific logs:
   ```bash
   WafSight -V 3 detect <url> | grep "CloudFlare"
   ```

---

## See Also

- [CLI Reference](cli-reference.md) - Complete command documentation
- [Quick Start](quickstart.md) - Get started with WafSight
- [Library Integration](library-integration.md) - Advanced logging in code
