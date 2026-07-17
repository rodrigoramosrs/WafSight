![WafSight](https://raw.githubusercontent.com/rodrigoramosrs/wafsight/refs/heads/dev/docs/assets/logo.png)

# WafSight

### Detect WAFs & CDNs with confidence — built for speed, designed for .NET

[![NuGet Version](https://img.shields.io/nuget/v/WafSight?style=for-the-badge&logo=nuget&logoColor=white&label=NuGet&labelColor=1a1a1a&color=29A1DC)](https://www.nuget.org/packages/WafSight)
[![NuGet Downloads](https://img.shields.io/nuget/dt/WafSight?style=for-the-badge&logo=nuget&logoColor=white&label=Downloads&labelColor=1a1a1a&color=29A1DC)](https://www.nuget.org/packages/WafSight)
[![Build](https://img.shields.io/github/actions/workflow/status/rodrigoramosrs/wafsight/ci.yml?style=for-the-badge&logo=github&logoColor=white&label=Build&labelColor=1a1a1a)](https://github.com/rodrigoramosrs/wafsight/actions)
[![License: MIT](https://img.shields.io/badge/License-MIT-6e40c9?style=for-the-badge&labelColor=1a1a1a)](LICENSE)
[![.NET 10](https://img.shields.io/badge/.NET-10.0-512bd4?style=for-the-badge&logo=dotnet&logoColor=white&labelColor=1a1a1a)](https://dotnet.microsoft.com)
[![AOT](https://img.shields.io/badge/Native-AOT-26a65a?style=for-the-badge&labelColor=1a1a1a)](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot)

---

WafSight is a high-performance WAF/CDN detection engine for .NET. Analyze HTTP responses, DNS records, TLS certificates, and payload probes — then get a scored, ranked verdict with confidence levels.

Available as a **library** (NuGet), a **cross-platform CLI** (native AOT), and a **DI-ready service** for ASP.NET Core.

> Full documentation at [rodrigoramosrs.github.io/WafSight](https://rodrigoramosrs.github.io/WafSight/)

## Features

| | |
|---|---|
| **8 Providers** | CloudFlare · AWS · Akamai · Fastly · Azure · Imperva · Sucuri · F5 |
| **Generic Detection** | Payload probing — XSS, SQLi, LFI, XXE, RCE |
| **Evidence Scoring** | Weighted, tiered confidence with caveats |
| **DNS Analysis** | CNAME, A, NS, TXT record inspection |
| **Batch Mode** | Concurrent URL detection with concurrency control |
| **Resilient** | Built-in retry policies, timeouts, circuit breakers |
| **DI Ready** | First-class `IServiceCollection` integration |
| **Native AOT** | Zero-framework standalone binaries for all platforms |
| **CLI** | 4 verbosity levels, batch files, provider listing |

## Quick Start

### NuGet

```bash
dotnet add package WafSight
```

### Library

```csharp
using WafSight;

var client = new WafDetectorClient();
var result = await client.DetectAsync("https://example.com");

if (result.HasWaf)
{
    Console.WriteLine($"WAF: {result.Waf.Name} (confidence: {result.Waf.Confidence:P0})");
}

if (result.HasCdn)
{
    Console.WriteLine($"CDN: {result.Cdn.Name} (confidence: {result.Cdn.Confidence:P0})");
}
```

### CLI

```bash
# Quick detection
wafsight detect https://example.com

# Verbose output
wafsight -V 3 detect https://example.com

# Batch from file
wafsight batch urls.txt

# List providers
wafsight providers
```

### Dependency Injection

```csharp
services.AddWafDetector(options =>
{
    options.Timeout = TimeSpan.FromSeconds(15);
    options.EnableGenericDetection = true;
    options.EnableDnsAnalysis = true;
});
```

## Detection Architecture

```
+-------------------------------------------------+
|              WafDetectorClient                   |
|                                                  |
|  +--------------+    +-----------------------+   |
|  |  HTTP Client |----+  Response Collector   |   |
|  |  (resilient) |    +-----------+-----------+   |
|  +--------------+                |               |
|                                  | Headers /     |
|                    +-------------v-------------+  |
|                    |   Provider Registry        |  |
|                    |                            |  |
|  +-----------------+---------------------------+  |
|  |  CloudFlare ----|                            |  |
|  |  AWS ----------|    IDetectionProvider      |  |
|  |  Akamai -------|    (8 providers)           |  |
|  |  Fastly -------|                           |  |
|  |  Azure --------|                            |  |
|  |  Imperva ------|  Evidence Collector        |  |
|  |  Sucuri -------|  -> Evidence Scorer       |  |
|  |  F5 ----------|   -> Confidence Ranking    |  |
|  |  Generic ------|  -> Detection Result      |  |
|  +-----------------+---------------------------+  |
|                                                  |
|  +-------------------------------------------+   |
|  |  Evidence Scorer  |  DNS Analyzer         |   |
|  |  (weights + tiers) |  (CNAME, A, NS, TXT) |   |
|  +-------------------------------------------+   |
|                                                  |
|                    +------------------+          |
|                    | DetectionResult  |          |
|                    |  WAF + CDN       |          |
|                    |  Confidence      |          |
|                    |  Caveats         |          |
|                    +------------------+          |
+-------------------------------------------------+
```

## Supported Providers

| Provider | WAF | CDN | Key Signals |
|----------|:---:|:---:|-------------|
| **CloudFlare** | Yes | Yes | `cf-ray` header, cookies, challenge pages |
| **AWS** | Yes | Yes | `x-amz-cf-id`, `x-amz-cf-pop`, CloudFront |
| **Akamai** | Yes | Yes | `x-akamai-transformed`, server header |
| **Fastly** | Yes | Yes | `x-fastly-request-id`, surrogate keys |
| **Azure** | Yes | Yes | `azure-ref`, `X-ARR-LogId`, server header |
| **Imperva** | Yes | Yes | `X-CDN`, cookies, server fingerprint |
| **Sucuri** | Yes | Yes | `X-Sucuri-ID`, block page detection |
| **F5** | Yes | Yes | `BIG-IP` server, `Last-Mile`, cookies |

## Evidence Scoring

WafSight doesn't just detect — it scores confidence:

| Evidence Tier | Sources | Weight |
|---------------|---------|--------|
| **Tier 1** (Highest) | HTTP Headers, TLS Certificates, DNS | 0.9 - 1.0 |
| **Tier 2** (Strong) | Cookies, Response Status Codes | 0.7 - 0.8 |
| **Tier 3** (Moderate) | Response Body Signatures | 0.4 - 0.6 |
| **Tier 4** (Weak) | Timing Analysis, Generic Probes | 0.1 - 0.3 |

Results include **caveats** when evidence is weak (e.g., body-only detection without header confirmation).

## CLI

```
Usage: wafsight [options] <command> [arguments]

Options:
  --verbose, -v, -V [0-3]  Verbosity level (0=None, 1=Low, 2=Medium, 3=High)

Commands:
  detect, -d <url>          Detect WAF/CDN for a single URL
  batch,   -b <file>        Detect WAF/CDN for URLs in a file (one per line)
  providers, -p             List all registered providers
  version                   Show version
  help,     -h              Show this help

Verbosity Levels:
  0 (None)   - Only errors and critical information
  1 (Low)    - Errors + basic status
  2 (Medium) - Low + headers, DNS records, provider scores
  3 (High)   - Medium + payload probing, evidence details, timing
```

### Binary Downloads

| Platform | AOT (Standalone) | Framework Dependent |
|----------|-----------------|---------------------|
| Windows x64 | `WafSight-win-x64.zip` | `WafSight-win-x64-framework.zip` |
| Linux x64 | `WafSight-linux-x64.tar.gz` | `WafSight-linux-x64-framework.tar.gz` |
| macOS x64 | `WafSight-osx-x64.tar.gz` | `WafSight-osx-x64-framework.tar.gz` |
| macOS ARM64 | `WafSight-osx-arm64.tar.gz` | `WafSight-osx-arm64-framework.tar.gz` |

> **AOT** binaries are native, self-contained executables (~9 MB). **Framework Dependent** requires [.NET 10 Runtime](https://dotnet.microsoft.com/download).
>
> Download from [GitHub Releases](https://github.com/rodrigoramosrs/wafsight/releases).

## Requirements

| Component | Minimum |
|-----------|---------|
| **.NET** | 10.0 |
| **AOT Binary** | None (standalone) |
| **Framework Binary** | .NET 10 Runtime |
| **Platforms** | Windows, Linux, macOS |

## Special Thanks

This project was inspired by outstanding open-source work:

| Project | Author | Contribution |
|---------|--------|--------------|
| [**waf-detector**](https://github.com/ammarion/waf-detector) | Ammar Atef | Architecture and provider model |
| [**wafw00f**](https://github.com/EnableSecurity/wafw00f) | EnableSecurity | Signature-based detection approach |

Open source thrives on community. If WafSight is useful to you, consider supporting these projects too.

---

*MIT License · Built for .NET*
