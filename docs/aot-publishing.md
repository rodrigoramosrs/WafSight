# AOT Publishing

Guide to publishing WafSight as an Ahead-of-Time (AOT) compiled native executable.

## Overview

AOT (Ahead-of-Time) compilation transforms .NET assemblies into native executables that:
- **Don't require .NET runtime** - Standalone executables
- **Start faster** - No JIT compilation at runtime
- **Have smaller footprint** - Only necessary code is included
- **Are platform-specific** - Need separate builds per OS/architecture

## Requirements

- .NET 8.0 SDK (preview or later)
- Target platform SDK installed:
  - **Windows**: Windows SDK
  - **Linux**: Linux development tools
  - **macOS**: Xcode (for macOS builds)

## Building AOT Executables

### Windows x64

```bash
dotnet publish src/WafSight.Cli/WafSight.Cli.csproj \
  -c Release \
  -r win-x64 \
  -p:Aot=true \
  -p:StripSymbols=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:PublishSingleFile=true \
  -o publish/win-x64
```

**Output:** `publish/win-x64/WafSightCli.exe` (~8-10 MB)

### Linux x64

```bash
dotnet publish src/WafSight.Cli/WafSight.Cli.csproj \
  -c Release \
  -r linux-x64 \
  -p:Aot=true \
  -p:StripSymbols=true \
  -p:PublishSingleFile=true \
  -o publish/linux-x64
```

**Output:** `publish/linux-x64/WafSightCli` (~5-8 MB)

### macOS x64

```bash
dotnet publish src/WafSight.Cli/WafSight.Cli.csproj \
  -c Release \
  -r osx-x64 \
  -p:Aot=true \
  -p:StripSymbols=true \
  -p:PublishSingleFile=true \
  -o publish/osx-x64
```

**Output:** `publish/osx-x64/WafSightCli` (~5-8 MB)

### macOS ARM64 (Apple Silicon)

```bash
dotnet publish src/WafSight.Cli/WafSight.Cli.csproj \
  -c Release \
  -r osx-arm64 \
  -p:Aot=true \
  -p:StripSymbols=true \
  -p:PublishSingleFile=true \
  -o publish/osx-arm64
```

**Output:** `publish/osx-arm64/WafSightCli` (~5-8 MB)

## Publish Flags Explained

### -p:Aot=true

Enables AOT compilation. Required for native executable output.

### -p:PublishSingleFile=true

Bundles the executable into a single file (no external DLLs).

### -p:StripSymbols=true

Removes debug symbols to reduce binary size.

### -p:IncludeNativeLibrariesForSelfExtract=true

Includes native libraries for self-extraction (Windows only).

### -r <runtime-id>

Specifies the target runtime:
- `win-x64` - Windows 64-bit
- `linux-x64` - Linux 64-bit
- `osx-x64` - macOS Intel
- `osx-arm64` - macOS Apple Silicon

## Cross-Platform Building

### Important Limitation

**You cannot cross-compile AOT executables.** Each platform must be built on its native OS:

| Build On | Can Build For |
|----------|---------------|
| Windows | win-x64 only |
| Linux | linux-x64 only |
| macOS | osx-x64, osx-arm64 |

### Solution: Use CI/CD

Use GitHub Actions to build for all platforms:

```yaml
jobs:
  build:
    strategy:
      matrix:
        os: [windows-latest, ubuntu-latest, macos-latest]
    runs-on: ${{ matrix.os }}
    
    steps:
      - uses: actions/checkout@v4
      
      - name: Publish
        run: |
          dotnet publish src/WafSight.Cli/WafSight.Cli.csproj \
            -c Release \
            -r ${{ matrix.runtime }} \
            -p:Aot=true
```

## Testing AOT Executables

### Windows

```powershell
.\publish\win-x64\WafSightCli.exe --help
.\publish\win-x64\WafSightCli.exe detect https://example.com
```

### Linux/macOS

```bash
chmod +x publish/linux-x64/WafSightCli
./publish/linux-x64/WafSightCli --help
./publish/linux-x64/WafSightCli detect https://example.com
```

## Troubleshooting

### Error: "Cross-OS native compilation is not supported"

**Cause:** Trying to build Linux/macOS AOT on Windows.

**Solution:** Build on the target platform or use CI/CD.

### Error: "Method will always throw"

**Cause:** Reflection code not compatible with AOT.

**Solution:** 
- Add `[DynamicDependency]` attributes
- Avoid reflection where possible
- See [CLI Source Generation](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/il-trimmer)

### Large binary size

**Cause:** Including unnecessary assemblies.

**Solution:**
- Use `-p:StripSymbols=true`
- Check IL Linker trim analysis
- Remove unused packages

## Performance Comparison

| Metric | JIT | AOT |
|--------|-----|-----|
| First run | Slower (JIT) | Faster |
| Memory | Higher | Lower |
| Startup | Slower | Faster |
| Size | Smaller | Larger |
| Portability | .NET required | Standalone |

## See Also

- [Multi-Platform Builds](multi-platform.md) - Building for all platforms
- [CI/CD Overview](cicd-overview.md) - Automated publishing
- [GitHub Actions](github-actions.md) - CI/CD configuration
