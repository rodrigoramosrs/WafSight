using System.Net;
using System.Net.Sockets;
using WafSight.Models;

namespace WafSight.Http;

/// <summary>
/// DNS analyzer for detecting WAF/CDN via DNS records
/// </summary>
public class DnsAnalyzer
{
    /// <summary>
    /// Resolves DNS records for a URL
    /// </summary>
    public async Task<DnsInfo?> ResolveAsync(string url, CancellationToken cancellationToken = default)
    {
        try
        {
            var uri = new Uri(url);
            var host = uri.Host;

            var info = new DnsInfo();

            var aRecords = await Dns.GetHostEntryAsync(host);
            foreach (var ip in aRecords.AddressList)
            {
                info.ARecords.Add(ip.ToString());
            }

            foreach (var alias in aRecords.Aliases)
            {
                info.Cnames.Add(alias);
            }

            return info;
        }
        catch
        {
            return null;
        }
    }
}
