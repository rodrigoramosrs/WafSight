# Troubleshooting

Common issues and solutions for WafSight.

## CLI Issues

### Command Not Found

**Error:**
```
'WafSight' is not recognized as an internal or external command
```

**Solution:**
```bash
# Install global tool
dotnet tool install --global WafSight.Cli

# Or use full path
.\src\WafSight.Cli\bin\Debug\net8.0\waf-sight.exe
```

### No Output

**Error:**
```
WafSight detect https://example.com
(no output)
```

**Solution:**
```bash
# Check verbosity level
WafSight -V 1 detect https://example.com

# Check for errors
WafSight -V 3 detect https://example.com
```

**Common causes:**
- Verbosity level 0 (no output)
- Network error
- Invalid URL

### Invalid URL

**Error:**
```
Error: Invalid URL format
```

**Solution:**
```bash
# Include protocol
WafSight detect https://example.com  # Good
WafSight detect example.com          # Bad

# Check URL format
WafSight detect http://invalid url   # Bad (space in URL)
```

## Library Issues

### Package Not Found

**Error:**
```
NU1101: Unable to find package WafSight
```

**Solution:**
```bash
# Correct package name
dotnet add package WafSight.Core

# Check NuGet source
dotnet nuget list source
```

### Namespace Not Found

**Error:**
```
The type or namespace name 'WafSight' could not be found
```

**Solution:**
```csharp
// Add using statement
using WafSight;

// Check package reference
<ItemGroup>
  <PackageReference Include="WafSight.Core" Version="*" />
</ItemGroup>
```

### NullReferenceException

**Error:**
```
System.NullReferenceException: Object reference not set to an instance
```

**Solution:**
```csharp
// Check result
var result = await client.DetectAsync(url);

if (result == null)
{
    Console.WriteLine("Detection failed");
    return;
}

if (result.Waf != null)
{
    Console.WriteLine(result.Waf.Name);
}
```

## Detection Issues

### False Positive

**Problem:** WafSight detects WAF when none exists.

**Solution:**
```bash
# Use higher verbosity to see evidence
WafSight -V 3 detect https://example.com

# Check which evidence triggered detection
# Evidence: [Header] server = Apache (Confidence: 0.85)

# Disable specific detection methods
# (Edit custom provider)
```

**Common causes:**
- Generic headers (server, x-powered-by)
- TLS certificates
- DNS records

### False Negative

**Problem:** WafSight doesn't detect known WAF.

**Solution:**
```bash
# Use verbose logging
WafSight -V 3 detect https://target.com

# Check if provider is registered
WafSight providers

# Register custom provider if needed
# See: custom-providers.md
```

**Common causes:**
- Proprietary WAF not in provider list
- WAF uses custom headers
- WAF detection disabled

### Slow Detection

**Problem:** Detection takes too long.

**Solution:**
```csharp
// Reduce timeout
var client = new WafDetectorClient(timeout: TimeSpan.FromSeconds(5));

// Disable unused features
services.AddWafDetector(options =>
{
    options.EnableGenericDetection = false;
    options.EnableDnsAnalysis = false;
});

// Limit concurrency
var results = await client.DetectBatchAsync(urls, maxConcurrency: 5);
```

**Check:**
- Network latency
- Target server response time
- Provider count

## Network Issues

### Connection Timeout

**Error:**
```
System.TimeoutException: The operation was canceled due to timeout
```

**Solution:**
```csharp
// Increase timeout
var client = new WafDetectorClient(timeout: TimeSpan.FromSeconds(30));
```

### DNS Resolution Failed

**Error:**
```
System.Net.Sockets.SocketException: No such host is known
```

**Solution:**
```bash
# Check URL
WafSight detect https://invalid-url

# Check DNS
nslookup example.com
```

### SSL Certificate Error

**Error:**
```
System.Net.Http.HttpRequestException: The SSL connection could not be established
```

**Solution:**
```csharp
// For testing only (not recommended for production)
var handler = new HttpClientHandler
{
    ServerCertificateCustomValidationCallback = 
        HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
};

var client = new WafDetectorClient(handler);
```

## Provider Issues

### Provider Not Detected

**Problem:** Expected provider not detected.

**Solution:**
```bash
# Check provider is registered
WafSight providers

# Use verbose logging
WafSight -V 3 detect https://target.com

# Check provider configuration
# (See custom-providers.md)
```

### Custom Provider Not Working

**Problem:** Custom provider doesn't detect anything.

**Solution:**
```csharp
// Add logging to provider
public class MyProvider : IDetectionProvider
{
    private readonly ILogger<MyProvider> _logger;
    
    public MyProvider(ILogger<MyProvider> logger)
    {
        _logger = logger;
    }
    
    public Task<List<Evidence>> DetectAsync(DetectionContext context)
    {
        _logger.LogInformation("Detecting {Url}", context.Url);
        
        // Your detection logic
        
        return Task.FromResult(evidence);
    }
}
```

## CI/CD Issues

### Build Fails

**Error:**
```
Build FAILED.
```

**Solution:**
```bash
# Check .NET version
dotnet --version

# Required: .NET 8.0 or later

# Clean and rebuild
dotnet clean
dotnet build -c Release
```

### NuGet Publish Fails

**Error:**
```
error NU1101: Unable to find package WafSight.Core
```

**Solution:**
```bash
# Check NuGet API key
# Settings → Secrets → NUGET_API_KEY

# Check package version doesn't exist
# NuGet doesn't allow overwriting packages

# Check package name
# Must be: WafSight.Core
```

### GitHub Release Fails

**Error:**
```
Resource not accessible by integration
```

**Solution:**
```bash
# Check workflow permissions
# .github/workflows/ci.yml

# Ensure:
permissions:
  contents: write  # For creating releases
  packages: write  # For NuGet
```

## Debugging

### Enable Detailed Logging

```csharp
var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddConsole();
    builder.SetMinimumLevel(LogLevel.Debug);
});

var client = new WafDetectorClient(loggerFactory);
```

### Check Network Traffic

**Windows:**
```powershell
# Use Fiddler or Wireshark
# Monitor HTTP requests
```

**Linux/macOS:**
```bash
# Use curl with verbose output
curl -v https://example.com

# Use tcpdump
sudo tcpdump -i any -A port 443
```

### Check DNS Resolution

```bash
# Windows
nslookup example.com
dig example.com

# Linux/macOS
nslookup example.com
dig example.com
```

### Check TLS Certificate

```bash
# Windows
openssl s_client -connect example.com:443

# Linux/macOS
openssl s_client -connect example.com:443
```

## Getting Help

### Check Documentation

- [README.md](../README.md) - Project overview
- [CLI Reference](cli-reference.md) - CLI options
- [API Reference](api-reference.md) - API documentation

### Check Issues

- [GitHub Issues](https://github.com/rodrigoramosrs/wafsight/issues)
- Search before creating new issue

### Create Issue

**Include:**
- WafSight version
- .NET version
- Operating system
- Steps to reproduce
- Expected behavior
- Actual behavior
- Logs (with `-V 3`)

### Ask for Help

- [GitHub Discussions](https://github.com/rodrigoramosrs/wafsight/discussions)
- Include code examples
- Include error messages
- Include logs

## See Also

- [Performance Tips](performance.md) - Optimize detection
- [CLI Reference](cli-reference.md) - CLI options
- [Library Integration](library-integration.md) - Usage examples
