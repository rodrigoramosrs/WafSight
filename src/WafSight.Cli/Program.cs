using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using WafSight;

namespace WafSight.Cli;

public class Program
{
    public static Verbosity CurrentVerbosity { get; private set; } = Verbosity.None;

    [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2026:UsingMemberWithoutReflectionAnnotation", Justification = "Types are directly referenced")]
    [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2070:TargetTypeCannotBeNull", Justification = "Types are directly referenced")]

    public static async Task Main(string[] args)
    {
        CurrentVerbosity = ParseVerbosity(args);
        
        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(CurrentVerbosity switch
            {
                Verbosity.None => LogLevel.None,
                Verbosity.Low => LogLevel.Warning,
                Verbosity.Medium => LogLevel.Information,
                Verbosity.High => LogLevel.Debug,
                _ => LogLevel.None
            });
        });

        var logger = loggerFactory.CreateLogger<Program>();
        logger.LogInformation("WafSight CLI v{Version}", typeof(Program).Assembly.GetName().Version);

        var filteredArgs = FilterArgs(args);

        if (filteredArgs.Length == 0)
        {
            PrintUsage();
            return;
        }

        var command = filteredArgs[0].ToLower();

        switch (command)
        {
            case "--detect" or "-d" or "detect":
                if (filteredArgs.Length < 2)
                {
                    logger.LogError("URL required. Usage: WafSight detect <url>");
                    return;
                }
                await RunDetect(filteredArgs[1], loggerFactory);
                break;

            case "--batch" or "-b" or "batch":
                if (filteredArgs.Length < 2)
                {
                    logger.LogError("URLs file required. Usage: WafSight batch <file>");
                    return;
                }
                await RunBatch(filteredArgs[1], loggerFactory);
                break;

            case "--providers" or "-p" or "providers":
                ListProviders(loggerFactory);
                break;

            case "--version" or "version":
                Console.WriteLine("WafSight CLI v" + typeof(Program).Assembly.GetName().Version);
                break;

            case "--help" or "-h" or "help":
                PrintUsage();
                break;

            default:
                logger.LogError("Unknown command: {Command}", command);
                PrintUsage();
                break;
        }
    }

    private static bool IsVerboseFlag(string arg) =>
        "--verbose".Equals(arg, StringComparison.OrdinalIgnoreCase) ||
        "-V".Equals(arg, StringComparison.OrdinalIgnoreCase) ||
        "-v".Equals(arg, StringComparison.OrdinalIgnoreCase);

    private static string[] FilterArgs(string[] args)
    {
        var filtered = new List<string>();
        for (int i = 0; i < args.Length; i++)
        {
            if (IsVerboseFlag(args[i]))
            {
                if (i + 1 < args.Length && int.TryParse(args[i + 1], out int level) && level >= 0 && level <= 3)
                {
                    i++;
                }
                continue;
            }
            filtered.Add(args[i]);
        }
        return filtered.ToArray();
    }

    private static Verbosity ParseVerbosity(string[] args)
    {
        for (int i = 0; i < args.Length; i++)
        {
            if (IsVerboseFlag(args[i]))
            {
                if (i + 1 < args.Length && int.TryParse(args[i + 1], out int level) && level >= 0 && level <= 3)
                {
                    return (Verbosity)level;
                }
                return Verbosity.High;
            }
        }
        return Verbosity.None;
    }

    private static async Task RunDetect(string url, ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger<WafDetectorClient>();
        logger.LogInformation("Starting detection for: {Url}", url);

        using var client = new WafDetectorClient(loggerFactory);

        var result = await client.DetectAsync(url);

        Console.WriteLine(result.ToSummary());

        if (CurrentVerbosity >= Verbosity.Medium && result.ProviderScores.Count > 0)
        {
            Console.WriteLine("\nProvider Scores:");
            foreach (var (name, score) in result.ProviderScores.OrderByDescending(s => s.Value))
            {
                Console.WriteLine("  " + name.PadRight(20) + score.ToString("P0"));
            }
        }

        if (CurrentVerbosity >= Verbosity.High && result.Evidence.Count > 0)
        {
            Console.WriteLine("\nEvidence:");
            foreach (var evidence in result.Evidence)
            {
                Console.WriteLine("  [" + evidence.Method + "] " + evidence.Name + " = " + evidence.Value + " (" + evidence.Confidence.ToString("P0") + ")");
            }
        }

        logger.LogInformation("Detection completed for: {Url}, Found: {Found}", url, result.HasWaf || result.HasCdn);
    }

    private static async Task RunBatch(string filePath, ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger<Program>();
        
        if (!File.Exists(filePath))
        {
            logger.LogError("File not found: {FilePath}", filePath);
            return;
        }

        var urls = await File.ReadAllLinesAsync(filePath);
        urls = urls.Where(u => !string.IsNullOrWhiteSpace(u) && !u.StartsWith("#")).ToArray();

        if (urls.Length == 0)
        {
            logger.LogWarning("No URLs found in file: {FilePath}", filePath);
            return;
        }

        logger.LogInformation("Starting batch scan of {Count} URLs from: {File}", urls.Length, filePath);

        using var client = new WafDetectorClient(loggerFactory);

        var results = await client.DetectBatchAsync(urls, maxConcurrency: 5);

        if (CurrentVerbosity >= Verbosity.Low)
        {
            Console.WriteLine("URL".PadRight(45) + " WAF".PadRight(15) + " CDN".PadRight(15) + " Time".PadLeft(8));
            Console.WriteLine(new string('-', 90));

            foreach (var (url, result) in results)
            {
                var wafName = result.HasWaf ? result.Waf!.Name : "-";
                var cdnName = result.HasCdn ? result.Cdn!.Name : "-";
                Console.WriteLine(url.PadRight(45) + wafName.PadRight(15) + cdnName.PadRight(15) + result.DetectionTimeMs.ToString().PadLeft(8) + "ms");
            }
        }

        logger.LogInformation("Batch scan completed. Total: {Total}, Found WAF: {WafCount}, Found CDN: {CdnCount}",
            results.Count(), results.Count(r => r.Value.HasWaf), results.Count(r => r.Value.HasCdn));
    }

    private static void ListProviders(ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger<Program>();
        logger.LogInformation("Listing registered providers");

        using var client = new WafDetectorClient(loggerFactory);

        if (CurrentVerbosity >= Verbosity.Low)
        {
            Console.WriteLine("Registered Providers:\n");
            Console.WriteLine("Name".PadRight(15) + " Type".PadRight(10) + " Priority".PadRight(10) + " Description".PadRight(40));
            Console.WriteLine(new string('-', 80));

            foreach (var provider in client.ListProviders())
            {
                Console.WriteLine(provider.Name.PadRight(15) + ((int)provider.ProviderType).ToString().PadRight(10) + provider.Priority.ToString().PadRight(10) + provider.Description.PadRight(40));
            }
        }

        Console.WriteLine("\nTotal: " + client.GetProviderCount() + " providers");
        logger.LogDebug("Provider count: {Count}", client.GetProviderCount());
    }

    private static void PrintUsage()
    {
        Console.WriteLine(@"Usage: WafSight [options] <command> [arguments]

Options:
  --verbose, -v, -V [0-3]  Set verbosity level (0=None, 1=Low, 2=Medium, 3=High)
                           Without a value defaults to 3 (High)
                           Default: 0 (None)

Commands:
  detect, -d <url>          Detect WAF/CDN for a single URL
  batch,   -b <file>        Detect WAF/CDN for URLs in a file (one per line)
  providers, -p             List all registered providers
  version                   Show version
  help,     -h              Show this help

Verbosity Levels:
  0 (None)   - Only errors and critical information
  1 (Low)    - Errors + basic status (detection results)
  2 (Medium) - Low + headers, DNS records, provider scores
  3 (High)   - Medium + payload probing, evidence details, timing

Examples:
  WafSight detect https://example.com
  WafSight --verbose 2 detect https://example.com
  WafSight -v 3 detect https://example.com
  WafSight -V 3 detect https://example.com
  WafSight --VERBOSE detect https://example.com
  WafSight batch urls.txt
  WafSight providers");
    }
}
