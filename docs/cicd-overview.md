# CI/CD Overview

Automated build, test, and release pipeline for WafSight.

## Overview

WafSight uses GitHub Actions for continuous integration and deployment:

- **Every push to main**: Build, test, publish NuGet, create GitHub Release
- **Pull requests**: Build and test only (no publishing)
- **Automatic versioning**: YYYY.M.0.MINOR format

## Pipeline Stages

```
┌─────────────┐     ┌─────────────┐     ┌─────────────┐
│  Build &    │────▶│  Publish    │────▶│  Create     │
│  Test       │     │  NuGet      │     │  Release    │
└─────────────┘     └─────────────┘     └─────────────┘
       │                                       │
       ▼                                       ▼
┌─────────────┐                     ┌──────────────────┐
│ Calculate   │                     │  Upload Artifacts│
│ Version     │                     │  (4 platforms)   │
└─────────────┘                     └──────────────────┘
```

## Jobs

### 1. Calculate Version

**Trigger:** Every push to main

**Purpose:** Calculate version based on existing releases

**Logic:**
```
Version = {YEAR}.{MONTH}.0.{MINOR}
MINOR = count of existing releases for same year.month + 1
```

**Example:**
- First release of July 2026: `2026.7.0.1`
- Second release of July 2026: `2026.7.0.2`
- First release of August 2026: `2026.8.0.1`

### 2. Build & Test

**Trigger:** After version calculation

**Runs on:** `windows-latest`

**Steps:**
1. Checkout code
2. Setup .NET 8.0
3. Restore packages
4. Build solution
5. Run tests (xUnit)
6. Upload test results

**Conditions:**
- PRs: Build & test only
- Main pushes: Continue to publishing

### 3. Publish NuGet

**Trigger:** Main push (after successful build & test)

**Runs on:** `ubuntu-latest`

**Steps:**
1. Checkout code
2. Setup .NET
3. Pack NuGet package
4. Push to NuGet.org

**Artifacts:**
- `WafSight.Core.{version}.nupkg`
- `WafSight.Core.{version}.snupkg` (symbols)

### 4. Publish Release

**Trigger:** Main push (after successful build & test)

**Runs on:** `ubuntu-latest`

**Steps:**
1. Checkout code
2. Setup .NET
3. Publish AOT for 4 platforms:
   - `win-x64`
   - `linux-x64`
   - `osx-x64`
   - `osx-arm64`
4. Package archives
5. Create GitHub Release
6. Upload artifacts

**Artifacts:**
- `WafSight-win-x64.zip`
- `WafSight-linux-x64.tar.gz`
- `WafSight-osx-x64.tar.gz`
- `WafSight-osx-arm64.tar.gz`

## Workflow File

Location: `.github/workflows/ci.yml`

## Required Secrets

### NUGET_API_KEY

**Purpose:** Authenticate with NuGet.org

**How to get:**
1. Go to [nuget.org](https://www.nuget.org/)
2. Sign in → Account → API Keys
3. Create new key (scoping: Push)
4. Copy key

**Setup in GitHub:**
```
Settings → Secrets and variables → Actions → New repository secret
Name: NUGET_API_KEY
Secret: <your-nuget-api-key>
```

## Required Environments

### nuget-publish

**Purpose:** Protect NuGet publishing

**Setup:**
```
Settings → Environments → New environment
Name: nuget-publish
Deployment branches: main
Required reviewers: (optional)
```

### github-release

**Purpose:** Protect release creation

**Setup:**
```
Settings → Environments → New environment
Name: github-release
Deployment branches: main
Required reviewers: (optional)
```

## Branch Protection

Recommended settings for `main` branch:

```
Settings → Branches → main → Add rule:

☑ Require pull request reviews
☑ Require status checks to pass
☑ Require branches to be up to date
☑ Include administrators
☑ Do not allow bypassing the above settings
```

**Required status checks:**
- `build-and-test`

## Version Calculation Details

### Algorithm

```python
def calculate_version():
    year = datetime.now().year
    month = datetime.now().month
    
    # Get existing releases for this month
    existing = gh_release_list(
        filter=f"^'{year}'.'{month}'\\.\\d+\\.\\d+$"
    )
    
    if not existing:
        minor = 1
    else:
        last = sorted(existing, key=version_key)[-1]
        minor = parse_minor(last) + 1
    
    return f"{year}.{month}.0.{minor}"
```

### Examples

| Date | Existing Releases | New Version |
|------|------------------|-------------|
| 2026-07-01 | None | `2026.7.0.1` |
| 2026-07-15 | `2026.7.0.1` | `2026.7.0.2` |
| 2026-07-31 | `2026.7.0.5` | `2026.7.0.6` |
| 2026-08-01 | `2026.7.0.6` | `2026.8.0.1` |

## Manual Triggers

You can manually trigger the workflow:

```
Actions → Build, Test & Release → Run workflow
```

Select branch and click "Run workflow".

## Monitoring

### Check Run Status

```
Actions → Build, Test & Release
```

View:
- Build status
- Test results
- Publish status
- Release creation

### View Logs

Click on each job to view detailed logs.

### Download Artifacts

After workflow completes:
1. Go to workflow run
2. Scroll to bottom
3. Download artifacts

## Troubleshooting

### Build Fails

**Check:**
- .NET version matches (`8.0.x`)
- All tests passing
- No breaking changes

### NuGet Publish Fails

**Check:**
- `NUGET_API_KEY` secret is set
- Package version doesn't exist on NuGet
- Package name is `WafSight.Core`

### Release Creation Fails

**Check:**
- Tag doesn't already exist
- GitHub token has `contents: write` permission
- All artifacts uploaded successfully

## See Also

- [GitHub Actions](github-actions.md) - Workflow configuration
- [Versioning](versioning.md) - Version calculation details
- [AOT Publishing](aot-publishing.md) - Native executable builds
