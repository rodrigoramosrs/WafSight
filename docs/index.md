# WafSight Documentation

Welcome to the WafSight documentation. This guide covers everything you need to know about using WafSight as a library, CLI tool, or in CI/CD pipelines.

## Quick Navigation

### Getting Started
- [README](../README.md) - Overview, quick start, and project structure
- [Installation](installation.md) - Package installation and requirements
- [Quick Start Guide](quickstart.md) - Step-by-step tutorial

### CLI Usage
- [CLI Reference](cli-reference.md) - Complete command-line options and examples
- [Verbosity Levels](cli-verbosity.md) - Understanding log levels and debugging

### Library Integration
- [Library Integration](library-integration.md) - Using WafSight as a DLL
- [Dependency Injection](di-integration.md) - ASP.NET Core integration
- [Custom Providers](custom-providers.md) - Creating your own detection providers
- [API Reference](api-reference.md) - Complete API documentation

### AOT & Publishing
- [AOT Publishing](aot-publishing.md) - Native executable compilation
- [Multi-Platform Builds](multi-platform.md) - Building for Windows, Linux, macOS

### Advanced Topics
- [Detection Methods](detection-methods.md) - How detection works internally
- [Provider Architecture](provider-architecture.md) - Extending the system
- [Performance Tips](performance.md) - Optimizing detection speed
- [Troubleshooting](troubleshooting.md) - Common issues and solutions

### Contributing
- [Contributing Guide](contributing.md) - How to contribute to WafSight
- [Code of Conduct](../CODE_OF_CONDUCT.md)

## Documentation Structure

```
docs/
├── index.md                    # This file (documentation index)
├── installation.md             # Package installation
├── quickstart.md               # Step-by-step tutorial
├── cli-reference.md            # CLI commands and options
├── cli-verbosity.md            # Logging and verbosity levels
├── library-integration.md      # Using as a DLL
├── di-integration.md           # Dependency injection
├── custom-providers.md         # Creating custom providers
├── api-reference.md            # Complete API docs
├── aot-publishing.md           # AOT native compilation
├── multi-platform.md           # Cross-platform builds
├── detection-methods.md        # Detection internals
├── provider-architecture.md    # Provider system
├── performance.md              # Performance optimization
├── troubleshooting.md          # FAQ and solutions
└── contributing.md             # Contributing guide

assets/
├── architecture.png            # System architecture diagram
├── detection-flow.png          # Detection flowchart
└── ci-cd-pipeline.png          # CI/CD pipeline diagram
```

## Related Documentation

- [README.md](../README.md) - Project overview
- [GitHub Repository](https://github.com/rodrigoramosrs/wafsight)
- [NuGet Package](https://www.nuget.org/packages/WafSight)

## Support

- **Issues**: [GitHub Issues](https://github.com/rodrigoramosrs/wafsight/issues)
- **Discussions**: [GitHub Discussions](https://github.com/rodrigoramosrs/wafsight/discussions)
- **License**: [MIT](../LICENSE)
