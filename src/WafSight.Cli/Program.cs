using WafSight;
using WafSight.Models;

namespace WafSight.Cli;

public class Program
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine("=== WafSight - WAF/CDN Detector ===\n");

        if (args.Length == 0)
        {
            PrintUsage();
            return;
        }

        var command = args[0].ToLower();

        switch (command)
        {
            case "--detect" or "-d":
                if (args.Length < 2)
                {
                    Console.WriteLine("Error: URL required. Usage: wafsight detect <url>");
                    return;
                }
                await RunDetect(args[1]);
                break;

            case "--batch" or "-b":
                if (args.Length < 2)
                {
                    Console.WriteLine("Error: URLs file required. Usage: wafsight batch <file>");
                    return;
                }
                await RunBatch(args[1]);
                break;

            case "--providers" or "-p":
                ListProviders();
                break;

            case "--version" or "-v":
                Console.WriteLine("WafSight CLI v2.0.0");
                break;

            case "--help" or "-h":
            case "help":
                PrintUsage();
                break;

            default:
                Console.WriteLine($"Unknown command: {command}");
                PrintUsage();
                break;
        }
    }

    private static async Task RunDetect(string url)
    {
        Console.WriteLine($"Scanning: {url}\n");

        using var client = new WafDetectorClient();

        var result = await client.DetectAsync(url);

        Console.WriteLine(result.ToSummary());

        if (result.ProviderScores.Count > 0)
        {
            Console.WriteLine("\nProvider Scores:");
            foreach (var (name, score) in result.ProviderScores.OrderByDescending(s => s.Value))
            {
                Console.WriteLine($"  {name,-20} {score:P0}");
            }
        }

        if (result.Evidence.Count > 0)
        {
            Console.WriteLine("\nEvidence:");
            foreach (var evidence in result.Evidence)
            {
                Console.WriteLine($"  [{evidence.Method}] {evidence.Name} = {evidence.Value} ({evidence.Confidence:P0})");
            }
        }
    }

    private static async Task RunBatch(string filePath)
    {
        if (!File.Exists(filePath))
        {
            Console.WriteLine($"Error: File not found: {filePath}");
            return;
        }

        var urls = await File.ReadAllLinesAsync(filePath);
        urls = urls.Where(u => !string.IsNullOrWhiteSpace(u) && !u.StartsWith("#")).ToArray();

        if (urls.Length == 0)
        {
            Console.WriteLine("No URLs found in file.");
            return;
        }

        Console.WriteLine($"Batch scanning {urls.Length} URLs...\n");

        using var client = new WafDetectorClient();

        var results = await client.DetectBatchAsync(urls, maxConcurrency: 5);

        Console.WriteLine($"{"URL",-45} {"WAF",-15} {"CDN",-15} {"Time",8}");
        Console.WriteLine(new string('-', 90));

        foreach (var (url, result) in results)
        {
            var wafName = result.HasWaf ? result.Waf!.Name : "-";
            var cdnName = result.HasCdn ? result.Cdn!.Name : "-";
            Console.WriteLine($"{url,-45} {wafName,-15} {cdnName,-15} {result.DetectionTimeMs,7}ms");
        }
    }

    private static void ListProviders()
    {
        using var client = new WafDetectorClient();

        Console.WriteLine("Registered Providers:\n");
        Console.WriteLine($"{"Name",-15} {"Type",-10} {"Priority",-10} {"Description",40}");
        Console.WriteLine(new string('-', 80));

        foreach (var provider in client.ListProviders())
        {
            Console.WriteLine($"{provider.Name,-15} {(int)provider.ProviderType,-10} {provider.Priority,-10} {provider.Description,40}");
        }

        Console.WriteLine($"\nTotal: {client.GetProviderCount()} providers");
    }

    private static void PrintUsage()
    {
        Console.WriteLine(@"Usage: wafsight <command> [options]

Commands:
  detect, -d <url>          Detect WAF/CDN for a single URL
  batch,   -b <file>        Detect WAF/CDN for URLs in a file (one per line)
  providers, -p             List all registered providers
  version,  -v              Show version
  help,     -h              Show this help

Examples:
  wafsight detect https://example.com
  wafsight batch urls.txt
  wafsight providers");
    }
}
