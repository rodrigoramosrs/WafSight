# GitHub Actions Workflow

Detailed configuration and customization of the GitHub Actions workflow.

## Workflow File

**Location:** `.github/workflows/ci.yml`

## Workflow Triggers

```yaml
on:
  push:
    branches: [main]
  pull_request:
```

**Behavior:**
- **Push to main**: Full pipeline (build, test, publish NuGet, create release)
- **Pull request**: Build & test only (no publishing)

## Concurrency Control

```yaml
concurrency:
  group: ${{ github.workflow }}-${{ github.ref }}
  cancel-in-progress: ${{ github.ref != 'refs/heads/main' }}
```

**Purpose:**
- Prevent parallel runs on same branch
- Cancel in-progress PR runs when new commit pushed
- Allow parallel main runs (different jobs)

## Environment Variables

```yaml
env:
  DOTNET_VERSION: '8.0.x'
  CONFIGURATION: Release
```

**Customization:**
- Change `.NET` version for all jobs
- Switch between `Release` and `Debug`

## Job: calculate-version

### Purpose
Calculate version based on existing GitHub releases.

### Steps

1. **Checkout** - Get code
2. **Calculate version** - Query GitHub releases
3. **Output version** - Pass to other jobs

### Version Calculation Logic

```bash
YEAR=$(date +%Y)
MONTH=$(date -u +%-m)

# Get existing releases for this month
EXISTING=$(gh release list --limit 100 --json tagName |
  jq -r '.[] | select(.tagName | test("^'"$YEAR".'"$MONTH"'.\\d+\\.\\d+$")) | .tagName' |
  sort -V | tail -1)

if [ -z "$EXISTING" ]; then
  MINOR=1
else
  PREVIOUS_MINOR=$(echo "$EXISTING" | awk -F. '{print $4}')
  MINOR=$((PREVIOUS_MINOR + 1))
fi

VERSION="$YEAR.$MONTH.0.$MINOR"
```

### Output

```yaml
outputs:
  version: ${{ steps.version.outputs.version }}
```

## Job: build-and-test

### Purpose
Build solution and run tests.

### Configuration

```yaml
build-and-test:
  needs: calculate-version
  runs-on: windows-latest
  outputs:
    version: ${{ needs.calculate-version.outputs.version }}
```

### Steps

1. **Checkout** - Get code
2. **Setup .NET** - Install .NET 8.0
3. **Cache** - NuGet package cache
4. **Restore** - Restore packages
5. **Build** - Build solution
6. **Test** - Run xUnit tests
7. **Upload results** - Save test results
8. **Extract version** - Parse version from csproj

### Test Results

**Format:** TRX (Visual Studio test results)

**Upload:**
```yaml
- name: Upload test results
  uses: actions/upload-artifact@v4
  if: always()
  with:
    name: test-results
    path: test-results/**/*.trx
```

## Job: publish-nuget

### Purpose
Publish NuGet package to NuGet.org.

### Configuration

```yaml
publish-nuget:
  needs: [calculate-version, build-and-test]
  runs-on: ubuntu-latest
  if: github.ref == 'refs/heads/main' && github.event_name == 'push'
  environment: nuget-publish
```

### Conditions

- Must be on `main` branch
- Must be a push (not PR)
- Must have successful build & test

### Steps

1. **Checkout** - Get code
2. **Setup .NET** - Install .NET 8.0
3. **Cache** - NuGet package cache
4. **Pack** - Create NuGet package
5. **Publish** - Push to NuGet.org
6. **Upload** - Save package as artifact

### NuGet Publish Command

```bash
dotnet nuget push nupkg/WafSight.Core.${VERSION}.nupkg \
  --api-key "${{ secrets.NUGET_API_KEY }}" \
  --source https://api.nuget.org/v3/index.json \
  --skip-duplicate
```

### Required Secrets

**NUGET_API_KEY** - NuGet.org API key

## Job: publish-release

### Purpose
Create GitHub Release with AOT executables.

### Configuration

```yaml
publish-release:
  needs: [calculate-version, build-and-test]
  runs-on: ubuntu-latest
  if: github.ref == 'refs/heads/main' && github.event_name == 'push'
  environment: github-release
```

### Steps

1. **Checkout** - Get code
2. **Setup .NET** - Install .NET 8.0
3. **Cache** - NuGet package cache
4. **Restore** - Restore packages
5. **Publish win-x64** - Build Windows executable
6. **Publish linux-x64** - Build Linux executable
7. **Publish osx-x64** - Build macOS Intel executable
8. **Publish osx-arm64** - Build macOS ARM executable
9. **Package** - Create archives
10. **Create release** - GitHub Release
11. **Upload** - Save artifacts

### Publish Commands

**Windows:**
```bash
dotnet publish src/WafSight.Cli/WafSight.Cli.csproj \
  -c Release \
  -r win-x64 \
  -p:Aot=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:StripSymbols=true \
  -o publish/win-x64
```

**Linux/macOS:**
```bash
dotnet publish src/WafSight.Cli/WafSight.Cli.csproj \
  -c Release \
  -r linux-x64 \
  -p:Aot=true \
  -p:StripSymbols=true \
  -o publish/linux-x64
```

### Package Archives

**Windows:**
```bash
cd publish/win-x64
zip -r ../../../WafSight-win-x64.zip .
```

**Linux/macOS:**
```bash
tar -czf WafSight-linux-x64.tar.gz -C publish/linux-x64 .
```

### Create Release

Uses `softprops/action-gh-release@v2`:

```yaml
- uses: softprops/action-gh-release@v2
  with:
    tag_name: v${{ needs.calculate-version.outputs.version }}
    name: Release v${{ needs.calculate-version.outputs.version }}
    generate_release_notes: true
    draft: false
    prerelease: false
    files: |
      WafSight-win-x64.zip
      WafSight-linux-x64.tar.gz
      WafSight-osx-x64.tar.gz
      WafSight-osx-arm64.tar.gz
```

## Permissions

```yaml
permissions:
  contents: read
  packages: write
  actions: read
```

**Required for:**
- `contents: read` - Checkout code
- `packages: write` - Publish NuGet
- `actions: read` - Read workflow outputs

## Caching

```yaml
- name: Cache
  uses: actions/cache@v4
  with:
    path: ~/.nuget/packages
    key: ${{ runner.os }}-nuget-${{ hashFiles('**/*.csproj') }}
    restore-keys: |
      ${{ runner.os }}-nuget-
```

**Purpose:** Speed up builds by caching NuGet packages.

## Artifacts

### Test Results

**Name:** `test-results`

**Contains:** TRX files from xUnit

**Retention:** 90 days (default)

### NuGet Packages

**Name:** `nuget-packages`

**Contains:** `.nupkg` and `.snupkg` files

**Retention:** 90 days (default)

### Release Binaries

**Name:** `release-binaries`

**Contains:** 4 platform archives

**Retention:** 90 days (default)

## Customization

### Change .NET Version

Edit `env.DOTNET_VERSION`:
```yaml
env:
  DOTNET_VERSION: '9.0.x'  # .NET 9 when available
```

### Add More Platforms

Add to `publish-release` job:
```yaml
- name: Publish win-arm64
  run: |
    dotnet publish ... -r win-arm64 ...
```

### Skip NuGet Publishing

Remove or comment out `publish-nuget` job.

### Skip Release Creation

Remove or comment out `publish-release` job.

### Add Slack Notification

```yaml
- name: Notify Slack
  if: failure()
  uses: slackapi/slack-github-action@v1
  with:
    payload: |
      {
        "text": "WafSight CI/CD failed: ${{ github.server_url }}/${{ github.repository }}/actions/runs/${{ github.run_id }}"
      }
  env:
    SLACK_WEBHOOK_URL: ${{ secrets.SLACK_WEBHOOK }}
```

## Troubleshooting

### Workflow Fails Silently

**Check:**
- View workflow run logs
- Check job status
- Verify secrets are set

### Version Not Incrementing

**Check:**
- Existing releases exist
- GitHub CLI (`gh`) is installed
- Release tag format matches

### Publish Fails

**Check:**
- `NUGET_API_KEY` is valid
- Package version doesn't exist
- Network connectivity

## See Also

- [CI/CD Overview](cicd-overview.md) - Pipeline architecture
- [Versioning](versioning.md) - Version calculation
- [AOT Publishing](aot-publishing.md) - Native executable builds
