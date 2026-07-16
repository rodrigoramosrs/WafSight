# Multi-Platform Builds

Guide to building WafSight for Windows, Linux, and macOS.

## Overview

WafSight CLI is published as AOT native executables for multiple platforms:

| Platform | Runtime ID | Archive | Size |
|----------|-----------|---------|------|
| Windows x64 | `win-x64` | `.zip` | ~8-10 MB |
| Linux x64 | `linux-x64` | `.tar.gz` | ~5-8 MB |
| macOS x64 | `osx-x64` | `.tar.gz` | ~5-8 MB |
| macOS ARM64 | `osx-arm64` | `.tar.gz` | ~5-8 MB |

## Local Building

### Prerequisites

- .NET 10.0 SDK
- Target platform (cross-compilation not supported for AOT)

### Build All Platforms

**On Windows:**
```bash
# Windows only (cross-platform AOT not supported)
dotnet publish src/WafSight/WafSight.csproj \
  -c Release \
  -r win-x64 \
  -p:Aot=true \
  -p:PublishSingleFile=true \
  -p:StripSymbols=true \
  -o publish/win-x64
```

**On Linux:**
```bash
# Linux only
dotnet publish src/WafSight/WafSight.csproj \
  -c Release \
  -r linux-x64 \
  -p:Aot=true \
  -p:PublishSingleFile=true \
  -p:StripSymbols=true \
  -o publish/linux-x64
```

**On macOS:**
```bash
# macOS Intel and ARM
dotnet publish src/WafSight/WafSight.csproj \
  -c Release -r osx-x64 \
  -p:Aot=true -p:PublishSingleFile=true -p:StripSymbols=true \
  -o publish/osx-x64

dotnet publish src/WafSight/WafSight.csproj \
  -c Release -r osx-arm64 \
  -p:Aot=true -p:PublishSingleFile=true -p:StripSymbols=true \
  -o publish/osx-arm64
```

## CI/CD Building (GitHub Actions)

The recommended approach is using GitHub Actions to build for all platforms.

### Workflow Configuration

```yaml
jobs:
  build:
    runs-on: ubuntu-latest
    strategy:
      matrix:
        runtime: [win-x64, linux-x64, osx-x64, osx-arm64]
    
    steps:
      - uses: actions/checkout@v4
      
      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'
      
      - name: Publish ${{ matrix.runtime }}
        run: |
          dotnet publish src/WafSight/WafSight.csproj \
            -c Release \
            -r ${{ matrix.runtime }} \
            -p:Aot=true \
            -p:PublishSingleFile=true \
            -p:StripSymbols=true \
            -o publish/${{ matrix.runtime }}
      
      - name: Package
        run: |
          if [ "${{ matrix.runtime }}" = "win-x64" ]; then
            cd publish/${{ matrix.runtime }}
            zip -r ../../../WafSight-${{ matrix.runtime }}.zip .
          else
            tar -czf WafSight-${{ matrix.runtime }}.tar.gz -C publish/${{ matrix.runtime }} .
          fi
      
      - name: Upload artifact
        uses: actions/upload-artifact@v4
        with:
          name: WafSight-${{ matrix.runtime }}
          path: WafSight-${{ matrix.runtime }}.*
```

## Downloading Pre-built Releases

### From GitHub Releases

1. Go to [GitHub Releases](https://github.com/rodrigoramosrs/wafsight/releases)
2. Download the appropriate archive for your platform

### Using wget/curl

**Linux/macOS:**
```bash
# Download latest release
LATEST=$(curl -s https://api.github.com/repos/rodrigoramosrs/wafsight/releases/latest)
URL=$(echo $LATEST | grep -o 'https://.*linux-x64.*\.tar\.gz' | head -1)

curl -L -o WafSight.tar.gz $URL
tar -xzf WafSight.tar.gz
chmod +x WafSight
```

**PowerShell:**
```powershell
# Download latest release
$latest = Invoke-RestMethod https://api.github.com/repos/rodrigoramosrs/wafsight/releases/latest
$url = ($latest.assets | Where-Object { $_.name -like "*win-x64*" }).browser_download_url

Invoke-WebRequest -Uri $url -OutFile WafSight.zip
Expand-Archive WafSight.zip -DestinationPath .\WafSight
```

## Verification

### Check Version

```bash
./WafSight version
# Output: WafSight CLI v2026.7.0.1
```

### Run Detection

```bash
./WafSight detect https://example.com
```

### Check Help

```bash
./WafSight --help
```

## Platform-Specific Notes

### Windows

- Executable has `.exe` extension
- May trigger Windows SmartScreen warning (uncommon binary)
- Sign with code signing certificate for production

### Linux

- Requires execute permission: `chmod +x WafSight`
- May need `libicu` installed on some systems
- Tested on Ubuntu 20.04+, Debian 11+, CentOS 8+

### macOS

- May trigger "unidentified developer" warning
- Run once, then: System Preferences → Security → Allow
- Or use: `xattr -d com.apple.quarantine WafSight`
- Apple Silicon (ARM64) recommended for M1/M2/M3 chips

## File Size Optimization

### Reduce Binary Size

```bash
# Strip symbols
-p:StripSymbols=true

# Use Linker optimization
-p:IlcOptimizationPreference=Size

# Enable single file
-p:PublishSingleFile=true

# Remove unnecessary dependencies
# (review .csproj for unused packages)
```

### Typical Sizes

| Platform | Debug | Release (stripped) |
|----------|-------|-------------------|
| Windows x64 | ~25 MB | ~8 MB |
| Linux x64 | ~20 MB | ~6 MB |
| macOS x64 | ~20 MB | ~6 MB |
| macOS ARM64 | ~18 MB | ~5 MB |

## Troubleshooting

### "Cannot find or load shared library"

**Linux:** Install missing dependencies
```bash
# Ubuntu/Debian
sudo apt-get install libicu-dev libssl-dev

# CentOS/RHEL
sudo yum install libicu openssl
```

### "Exec format error"

**Cause:** Wrong architecture binary.

**Solution:** Download correct version for your platform.

### Windows SmartScreen warning

**Solution:** 
1. Click "More info" → "Run anyway"
2. Or sign with code signing certificate
3. Or add to Windows Defender exclusions

## See Also

- [AOT Publishing](aot-publishing.md) - AOT compilation details

