# Contributing Guide

Thank you for your interest in contributing to WafSight! This guide will help you get started.

## Table of Contents

- [Code of Conduct](#code-of-conduct)
- [Getting Started](#getting-started)
- [Development Setup](#development-setup)
- [Making Changes](#making-changes)
- [Testing](#testing)
- [Pull Requests](#pull-requests)
- [Coding Standards](#coding-standards)
- [Adding New Providers](#adding-new-providers)
- [Documentation](#documentation)

## Code of Conduct

This project follows the [Contributor Covenant Code of Conduct](CODE_OF_CONDUCT.md).

## Getting Started

### Prerequisites

- .NET 10.0 SDK or later
- Git
- IDE (Visual Studio, VS Code, Rider, etc.)

### Fork and Clone

```bash
# Fork the repository on GitHub
# Then clone your fork
git clone https://github.com/YOUR_USERNAME/wafsight.git
cd wafsight

# Add upstream remote
git remote add upstream https://github.com/rodrigoramosrs/wafsight.git
```

### Install Dependencies

```bash
dotnet restore
```

### Build

```bash
dotnet build -c Release
```

### Test

```bash
dotnet test -c Release
```

## Development Setup

### Project Structure

```
WafSight/
├── src/
│   ├── WafSight/              # Core library
│   │   ├── Analysis/          # Evidence scoring
│   │   ├── Extensions/        # DI extensions
│   │   ├── Http/              # HTTP client, DNS analyzer
│   │   ├── Models/            # Data models
│   │   ├── Providers/         # Detection providers
│   │   └── Registry/          # Provider registry
│   ├── WafSight/              # Core library + CLI tool
│   └── WafSight.Tests/        # Unit tests
├── docs/                      # Documentation
├── assets/                    # Images and assets
└── .github/                   # CI/CD workflows
```

### Key Files

- `src/WafSight/WafSight.csproj` - Library project
- `src/WafSight/WafSight.csproj` - Core library + CLI
- `src/WafSight.Tests/WafSight.Tests.csproj` - Test project
- `.github/workflows/ci.yml` - CI/CD workflow
- `README.md` - Project README

## Making Changes

### Create a Branch

```bash
# Create branch from main
git checkout main
git pull upstream main
git checkout -b feature/my-feature

# Or for bug fixes
git checkout -b fix/my-bug
```

### Make Your Changes

1. Make code changes
2. Add tests for new functionality
3. Update documentation if needed
4. Run tests locally

### Commit Changes

```bash
git add .
git commit -m "feat: add support for new WAF provider"
```

**Commit message format:**
- `feat:` - New feature
- `fix:` - Bug fix
- `docs:` - Documentation changes
- `chore:` - Build/tooling changes
- `refactor:` - Code refactoring

### Push Changes

```bash
git push origin feature/my-feature
```

## Testing

### Run All Tests

```bash
dotnet test -c Release
```

### Run Specific Test

```bash
dotnet test -c Release --filter "FullyQualifiedName~CloudFlare"
```

### Run Tests with Logging

```bash
dotnet test -c Release --logger "trx;LogFileName=test-results.trx"
```

### Test CLI

```bash
# Build CLI
dotnet build src/WafSight/WafSight.csproj -c Release

# Run CLI
.\src\WafSight\bin\Release\net10.0\WafSight.exe --help
```

## Pull Requests

### Create Pull Request

1. Go to [WafSight repository](https://github.com/rodrigoramosrs/wafsight)
2. Click "Compare & pull request"
3. Fill in PR template:
   - **Title:** Clear, descriptive title
   - **Description:** What changes, why, how to test
4. Click "Create pull request"

### PR Requirements

- [ ] Changes are tested
- [ ] Code follows standards
- [ ] Documentation updated
- [ ] No breaking changes (or clearly marked)
- [ ] CI checks pass

### PR Review Process

1. **Automated checks** - CI builds and tests
2. **Code review** - Maintainers review changes
3. **Feedback** - Address review comments
4. **Approval** - Maintainer approves
5. **Merge** - PR is merged to main

### PR Template

```markdown
## Description
Brief description of changes.

## Type of Change
- [ ] Bug fix
- [ ] New feature
- [ ] Documentation update
- [ ] Refactoring

## Testing
- [ ] Tests added/updated
- [ ] Manual testing done

## Checklist
- [ ] Code follows standards
- [ ] Documentation updated
- [ ] No breaking changes
```

## Coding Standards

### C# Standards

- **Nullable reference types:** Enabled
- **Implicit usings:** Enabled
- **Top-level statements:** Use when appropriate
- **Async/await:** Use for async operations

### Naming Conventions

```csharp
// Classes: PascalCase
public class WafDetectorClient { }

// Methods: PascalCase
public async Task<DetectionResult> DetectAsync(string url) { }

// Properties: PascalCase
public string Name { get; set; }

// Private fields: _camelCase
private readonly ILogger<WafDetectorClient> _logger;

// Parameters: camelCase
public WafDetectorClient(ILoggerFactory? loggerFactory = null)

// Constants: PascalCase or UPPER_SNAKE_CASE
public const int MaxRetries = 3;
```

### Code Style

**Use expression bodies when simple:**
```csharp
// Good
public string Name => "WafSight";

// Bad
public string Name { get { return "WafSight"; } }
```

**Use pattern matching:**
```csharp
// Good
if (response is not null)
{
    // ...
}

// Bad
if (response != null)
{
    // ...
}
```

**Use string interpolation:**
```csharp
// Good
Console.WriteLine($"URL: {url}");

// Bad
Console.WriteLine("URL: " + url);
```

### XML Documentation

**Document public APIs:**
```csharp
/// <summary>
/// Detects WAF/CDN for a single URL.
/// </summary>
/// <param name="url">Target URL to detect</param>
/// <param name="cancellationToken">Optional cancellation token</param>
/// <returns>Detection result</returns>
public Task<DetectionResult> DetectAsync(
    string url, 
    CancellationToken cancellationToken = default)
```

**Document complex logic:**
```csharp
// Calculate confidence score based on weighted evidence
// Tier 1 evidence receives 1.5x bonus
var weightedScore = evidence.Confidence * evidence.Weight;
if (evidence.Tier == Tier1)
{
    weightedScore *= 1.5;
}
```

### Error Handling

**Use exceptions for errors:**
```csharp
if (provider == null)
{
    throw new ArgumentNullException(nameof(provider));
}
```

**Use result types for expected failures:**
```csharp
public Task<Result<DetectionResult>> DetectAsync(string url)
{
    try
    {
        var result = await client.DetectAsync(url);
        return Result.Success(result);
    }
    catch (Exception ex)
    {
        return Result.Failure(ex);
    }
}
```

## Adding New Providers

### 1. Create Provider Class

```csharp
// src/WafSight/Providers/MyProvider.cs
using WafSight;
using WafSight.Models;
using WafSight.Providers;

public class MyProvider : IDetectionProvider
{
    public string Name => "MyProvider";
    public string Version => "1.0.0";
    public string Description => "My custom WAF detection";
    public ProviderType ProviderType => ProviderType.WAF;
    public double ConfidenceBase => 0.85;
    public int Priority => 50;
    public bool Enabled => true;

    public Task<List<Evidence>> DetectAsync(DetectionContext context)
    {
        // Detection logic
    }

    public Task<List<Evidence>> PassiveDetectAsync(HttpResponseData response)
    {
        return DetectAsync(new DetectionContext { Response = response });
    }
}
```

### 2. Register Provider

```csharp
// src/WafSight/WafDetectorClient.cs
private void RegisterDefaultProviders()
{
    // ... existing providers ...
    _registry.RegisterProvider(new MyProvider(_logger));
}
```

### 3. Add Tests

```csharp
// src/WafSight.Tests/MyProviderTests.cs
[Fact]
public async Task DetectAsync_WithHeader_ReturnsEvidence()
{
    var provider = new MyProvider(null);
    
    var context = new DetectionContext
    {
        Response = new HttpResponseData
        {
            Headers = new Dictionary<string, string>
            {
                { "x-my-waf", "detected" }
            }
        }
    };
    
    var evidence = await provider.DetectAsync(context);
    
    Assert.Single(evidence);
}
```

### 4. Update Documentation

```markdown
## Built-in Providers

| Provider | Type | Priority |
|----------|------|----------|
| MyProvider | WAF | 50 |
```

## Documentation

### Update README

```markdown
# WafSight

High-performance WAF/CDN detection library and CLI for .NET.

## Features
- **8 built-in providers**: ...
```

### Update API Documentation

```csharp
/// <summary>
/// New method description.
/// </summary>
public Task<NewResult> NewMethod(string param)
```

### Add to docs/

```markdown
# New Feature Documentation

## Overview
...

## Usage
...

## Examples
...
```

## Building Documentation

Documentation is in Markdown format. No special build process needed.

**View locally:**
- Open `.md` files in any text editor
- Use GitHub to view rendered Markdown

**Deploy to GitHub Pages:**
```bash
# Add GitHub Pages workflow
# See: https://pages.github.com/
```

## Issue Guidelines

### Reporting Bugs

**Include:**
- WafSight version
- .NET version
- Operating system
- Steps to reproduce
- Expected behavior
- Actual behavior
- Logs (with `-V 3`)

**Example:**
```
## Bug Description
WafSight doesn't detect CloudFlare on some URLs.

## Steps to Reproduce
1. Run: WafSight detect https://example.com
2. Check output

## Expected Behavior
Should detect CloudFlare

## Actual Behavior
Shows "Not detected"

## Logs
WafSight -V 3 detect https://example.com
...
```

### Requesting Features

**Include:**
- Feature description
- Use case
- Expected behavior
- Alternatives considered

**Example:**
```
## Feature Request
Add support for Incapsula WAF detection.

## Use Case
Our organization uses Incapsula and we need to detect it.

## Expected Behavior
WafSight should detect Incapsula by checking specific headers.

## Alternatives
We could manually check headers, but WafSight would be better.
```

## Release Process

### Version Bump

1. Update version in `src/WafSight/WafSight.csproj`
2. Update CHANGELOG.md
3. Create release branch
4. Create PR
5. Merge to main
6. CI/CD creates release automatically

### Create Release

1. Go to [GitHub Releases](https://github.com/rodrigoramosrs/wafsight/releases)
2. Click "Draft a new release"
3. Fill in:
   - Tag: `v{version}`
   - Name: `Release v{version}`
   - Description: Changelog
4. Attach binaries (optional)
5. Click "Publish release"

## Getting Help

### Documentation
- [README.md](../README.md)
- [docs/](./)
- [API Reference](api-reference.md)

### Support
- [GitHub Issues](https://github.com/rodrigoramosrs/wafsight/issues)
- [GitHub Discussions](https://github.com/rodrigoramosrs/wafsight/discussions)

### Contact
- **Maintainer:** rodrigoramosrs
- **Email:** (see GitHub profile)

## Thank You!

Thank you for contributing to WafSight! Your contributions help make WAF/CDN detection better for everyone.
