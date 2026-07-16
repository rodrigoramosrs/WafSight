# Installation

This guide covers how to install WafSight in your project.

## Requirements

- **.NET 10.0** or later
- **Visual Studio 2022** (optional, for IDE support)
- **dotnet CLI** (for command-line builds)

Check your .NET version:
```bash
dotnet --version
```

## Installing the NuGet Package

### Using dotnet CLI

```bash
dotnet add package WafSight
```

### Using Package Manager Console (Visual Studio)

```
Install-Package WafSight
```

### Manual (edit .csproj)

Add to your project file:
```xml
<ItemGroup>
  <PackageReference Include="WafSight" Version="*" />
</ItemGroup>
```

## Installing the CLI Tool

### Global Tool Installation

Install WafSight as a global .NET tool:
```bash
dotnet tool install --global WafSight.Cli
```

After installation, use `WafSight` from anywhere:
```bash
WafSight detect https://example.com
```

### Download from GitHub Releases

1. Go to [GitHub Releases](https://github.com/rodrigoramosrs/wafsight/releases)
2. Download the appropriate archive for your platform:
   - `WafSight-win-x64.zip` - Windows
   - `WafSight-linux-x64.tar.gz` - Linux
   - `WafSight-osx-x64.tar.gz` - macOS (Intel)
   - `WafSight-osx-arm64.tar.gz` - macOS (Apple Silicon)

3. Extract and use:

**Windows:**
```powershell
Expand-Archive -Path WafSight-win-x64.zip -DestinationPath .\WafSight
.\WafSight\WafSight.exe detect https://example.com
```

**Linux/macOS:**
```bash
tar -xzf WafSight-linux-x64.tar.gz
chmod +x WafSight
./WafSight detect https://example.com
```

## Project Template

Create a new console app:
```bash
dotnet new console -n MyWafDetector
cd MyWafDetector
dotnet add package WafSight
```

## Next Steps

- [Quick Start Guide](quickstart.md) - Your first detection
- [CLI Reference](cli-reference.md) - Command-line options
- [Library Integration](library-integration.md) - Using as a DLL

## Version Compatibility

| WafSight Version | .NET Version | Status |
|-----------------|--------------|--------|
| 2.x.x | .NET 10.0 | Current |
| 1.x.x | .NET 6.0 | Legacy |

## Troubleshooting

### Package not found
Make sure you're using the correct package name: `WafSight` (not `WafSight`)

### .NET version too old
WafSight requires .NET 10.0. Install from [dotnet.microsoft.com](https://dotnet.microsoft.com/download)

### NuGet source issues
Ensure you have nuget.org configured:
```bash
dotnet nuget list source
```

If missing, add it:
```bash
dotnet nuget add source https://api.nuget.org/v3/index.json -n nuget.org
```
