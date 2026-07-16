# Versioning Strategy

Automatic version calculation for WafSight releases.

## Overview

WafSight uses an automatic versioning system based on the current date and release count:

**Format:** `YYYY.M.0.MINOR`

| Component | Description | Example |
|-----------|-------------|---------|
| `YYYY` | Current year | `2026` |
| `M` | Current month (1-12) | `7` |
| `0` | Major version (fixed) | `0` |
| `MINOR` | Incremental counter | `1`, `2`, `3`... |

## Examples

| Date | Release # | Version |
|------|-----------|---------|
| July 1, 2026 | 1st | `2026.7.0.1` |
| July 15, 2026 | 2nd | `2026.7.0.2` |
| July 31, 2026 | 10th | `2026.7.0.10` |
| August 1, 2026 | 1st of August | `2026.8.0.1` |
| December 31, 2026 | 50th | `2026.12.0.50` |

## Algorithm

### Pseudocode

```python
def calculate_version():
    year = current_year()
    month = current_month()
    
    # Get all GitHub releases
    releases = github_release_list()
    
    # Filter releases for current year.month
    monthly_releases = [
        r for r in releases 
        if r.tag_name matches f"^{year}.{month}.\\d+\\.\\d+$"
    ]
    
    # Sort by version
    monthly_releases.sort(key=parse_version)
    
    # Determine minor version
    if len(monthly_releases) == 0:
        minor = 1
    else:
        last = monthly_releases[-1]
        minor = parse_minor(last) + 1
    
    return f"{year}.{month}.0.{minor}"
```

### Bash Implementation

```bash
#!/bin/bash

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
echo "$VERSION"
```

## GitHub Actions Integration

### In Workflow

```yaml
- name: Calculate version
  id: version
  run: |
    YEAR=$(date +%Y)
    MONTH=$(date -u +%-m)
    
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
    echo "version=$VERSION" >> "$GITHUB_OUTPUT"
```

### Using Version in Other Jobs

```yaml
jobs:
  calculate-version:
    outputs:
      version: ${{ steps.version.outputs.version }}
  
  publish-nuget:
    needs: calculate-version
    steps:
      - name: Publish
        run: |
          dotnet nuget push *.nupkg \
            --version ${{ needs.calculate-version.outputs.version }}
```

## Version in Artifacts

### NuGet Package

**Filename:** `WafSight.Core.{version}.nupkg`

**Example:** `WafSight.Core.2026.7.0.1.nupkg`

### GitHub Release

**Tag:** `v{version}`

**Example:** `v2026.7.0.1`

**Name:** `Release v{version}`

**Example:** `Release v2026.7.0.1`

### CLI Output

```bash
WafSight version
# Output: WafSight CLI v2026.7.0.1
```

## Manual Version Override

### Force Specific Version

Edit `src/WafSight/WafSight.csproj`:

```xml
<Version>2026.7.0.99</Version>
```

**Note:** This is temporary. The workflow will recalculate on next push.

### Reset Counter

To reset MINOR counter for a month:

1. Delete existing releases for that month
2. Next push will calculate MINOR=1

## Version History

### Format Changes

| Era | Format | Example |
|-----|--------|---------|
| Before | `MAJOR.MINOR.PATCH` | `2.0.0` |
| Current | `YYYY.M.0.MINOR` | `2026.7.0.1` |

### Why This Format?

1. **Sortable** - Lexicographic sort works correctly
2. **Informative** - Shows when release was made
3. **Predictable** - Easy to understand progression
4. **Unique** - Never conflicts with existing versions

## Semantic Versioning Compatibility

While not strictly SemVer, this format is compatible:

- **Major**: Fixed at `0` (can change for breaking changes)
- **Minor**: Incremental per month
- **Patch**: Not used (would be `0`)

For breaking changes, consider:
- Creating a new major branch
- Or changing format to include major version

## Future Enhancements

### Pre-release Tags

```yaml
# For beta releases
VERSION="$YEAR.$MONTH.0.$MINOR-beta.1"
```

### Build Metadata

```yaml
# Include commit hash
VERSION="$YEAR.$MONTH.0.$MINOR+${{ github.sha }}"
```

### Date-based Minor

```yaml
# Use day of month instead of counter
MINOR=$(date +%d)
VERSION="$YEAR.$MONTH.0.$MINOR"
```

## Troubleshooting

### Version Not Incrementing

**Check:**
- Existing releases exist for current month
- GitHub CLI (`gh`) is installed in workflow
- Release tags match expected format

### Version Format Wrong

**Check:**
- Year and month extraction correct
- Regex pattern matches release tags
- Sorting works correctly

### Duplicate Versions

**Check:**
- No manual version overrides
- Workflow runs sequentially (not parallel)
- No concurrent pushes to main

## See Also

- [CI/CD Overview](cicd-overview.md) - Version calculation in pipeline
- [GitHub Actions](github-actions.md) - Workflow configuration
- [AOT Publishing](aot-publishing.md) - Version in artifacts
