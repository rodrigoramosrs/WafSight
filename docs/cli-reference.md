# CLI Reference

Complete documentation for the WafSight command-line interface.

## Overview

The WafSight CLI provides a powerful way to detect WAFs and CDNs from the command line. It supports single URL detection, batch processing, and detailed logging.

## Commands

### detect

Detect WAF/CDN for a single URL.

```bash
WafSight detect <url> [options]
```

**Aliases:** `-d`

**Examples:**
```bash
# Basic detection
WafSight detect https://example.com

# With verbose output
WafSight -V 2 detect https://example.com

# High verbosity (debug mode)
WafSight -V 3 detect https://cloudflare.com
```

**Output:**
```
URL: example.com
WAF: CloudFlare (95%)
CDN: CloudFlare (95%)
Evidence: 3 | Time: 45ms
```

### batch

Detect WAF/CDN for multiple URLs from a file.

```bash
WafSight batch <file> [options]
```

**Aliases:** `-b`

**File format:** One URL per line, `#` for comments:
```
# Target websites
https://example.com
https://cloudflare.com
# This line is ignored
https://aws.amazon.com
```

**Examples:**
```bash
# Basic batch detection
WafSight batch urls.txt

# With concurrency control
WafSight -V 1 batch urls.txt
```

**Output:**
```
URL                                       WAF           CDN           Time
------------------------------------------------------------------------------------------
https://example.com                       -             -             45ms
https://cloudflare.com                    CloudFlare    CloudFlare    52ms
https://aws.amazon.com                    AWS           AWS           48ms
```

### providers

List all registered detection providers.

```bash
WafSight providers
```

**Aliases:** `-p`

**Output:**
```
Registered Providers:

Name            Type      Priority  Description
--------------------------------------------------------------------------------
CloudFlare      3         100       CloudFlare WAF/CDN detection provider
AWS             3         95        AWS WAF and CloudFront CDN detection provider
Akamai          3         90        Akamai WAF/CDN detection provider
Fastly          2         85        Fastly CDN detection provider
Azure           3         80        Microsoft Azure WAF and CDN detection provider
Imperva         1         75        Imperva (Incapsula) WAF detection provider
Sucuri          1         70        Sucuri WAF detection provider
F5              1         65        F5 BIG-IP WAF detection provider

Total: 8 providers
```

### version

Show version information.

```bash
WafSight version
```

**Aliases:** `-v`

**Output:**
```
WafSight CLI v2026.7.0.1
```

### help

Show help information.

```bash
WafSight help
```

**Aliases:** `-h`

**Output:**
```
Usage: WafSight [options] <command> [arguments]

Options:
  --verbose, -V [0-3]       Set verbosity level (0=None, 1=Low, 2=Medium, 3=High)
                            Default: 0 (None)

Commands:
  detect, -d <url>          Detect WAF/CDN for a single URL
  batch,   -b <file>        Detect WAF/CDN for URLs in a file (one per line)
  providers, -p             List all registered providers
  version,  -v              Show version
  help,     -h              Show this help

Verbosity Levels:
  0 (None)   - Only errors and critical information
  1 (Low)    - Errors + basic status (detection results)
  2 (Medium) - Low + headers, DNS records, provider scores
  3 (High)   - Medium + payload probing, evidence details, timing

Examples:
  WafSight detect https://example.com
  WafSight -V 2 detect https://example.com
  WafSight batch urls.txt
  WafSight providers
```

## Options

### --verbose, -V

Set verbosity level for logging output.

**Syntax:**
```bash
WafSight -V <level> <command> [args]
WafSight --verbose <level> <command> [args]
```

**Levels:**

| Level | Name | Description | Use Case |
|-------|------|-------------|----------|
| `0` | None | Only errors | Production scripts |
| `1` | Low | Errors + results | Basic monitoring |
| `2` | Medium | + Headers, DNS, scores | Debugging detections |
| `3` | High | + Payloads, evidence, timing | Development & testing |

**Examples:**
```bash
# Silent mode (production)
WafSight -V 0 detect https://example.com

# Show results only
WafSight -V 1 detect https://example.com

# Show headers and scores
WafSight -V 2 detect https://example.com

# Full debug output
WafSight -V 3 detect https://example.com
```

## Exit Codes

| Code | Description |
|------|-------------|
| `0` | Success |
| `1` | Error (invalid arguments, network failure, etc.) |

## Examples

### Basic Detection
```bash
WafSight detect https://cloudflare.com
```

### Batch with Logging
```bash
WafSight -V 2 batch targets.txt > results.log
```

### Check Providers
```bash
WafSight providers
```

### Version Check
```bash
WafSight version
```

## Integration with Other Tools

### Pipe to JSON (future)
```bash
WafSight detect https://example.com | jq '.WAF'
```

### Use in Scripts
```bash
#!/bin/bash
for url in $(cat urls.txt); do
    result=$(WafSight -V 0 detect "$url")
    echo "$result" >> results.txt
done
```

### PowerShell Integration
```powershell
$results = WafSight batch urls.txt
$results | Out-GridView
```

## Tips

1. **Use `-V 0` for scripts** - Clean output without logs
2. **Use `-V 2` for debugging** - See what's being detected
3. **Batch files support comments** - Use `#` for notes
4. **Combine with grep** - Filter results in large batches

## See Also

- [Quick Start](quickstart.md) - Get started with WafSight
- [CLI Verbosity](cli-verbosity.md) - Detailed verbosity documentation
- [Library Integration](library-integration.md) - Using WafSight as a DLL
