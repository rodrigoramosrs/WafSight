using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using WafSight.Models;

namespace WafSight.Http;

/// <summary>
/// DNS analyzer for detecting WAF/CDN via DNS records
/// </summary>
public class DnsAnalyzer
{
    private readonly ILogger<DnsAnalyzer>? _logger;

    public DnsAnalyzer(ILogger<DnsAnalyzer>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Resolves DNS records for a URL
    /// </summary>
    public async Task<DnsInfo?> ResolveAsync(string url, CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("Starting DNS resolution for {Url}", url);

        try
        {
            var uri = new Uri(url);
            var host = uri.Host;
            _logger?.LogDebug("Extracted host {Host} from URL", host);

            var info = new DnsInfo();

            var aRecords = await Dns.GetHostEntryAsync(host);
            foreach (var ip in aRecords.AddressList)
            {
                info.ARecords.Add(ip.ToString());
                _logger?.LogDebug("A record found: {Ip}", ip);
            }

            foreach (var alias in aRecords.Aliases)
            {
                info.Cnames.Add(alias);
                _logger?.LogDebug("CNAME alias found: {Alias}", alias);
            }

            _logger?.LogInformation("DNS resolution completed for {Url}: {ACount} A records, {CCount} CNAME records",
                url, info.ARecords.Count, info.Cnames.Count);

            return info;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "DNS resolution failed for {Url}", url);
            return null;
        }
    }
}
