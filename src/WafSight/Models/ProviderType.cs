namespace WafSight.Models;

/// <summary>
/// Provider type
/// </summary>
[Flags]
public enum ProviderType
{
    /// <summary>
    /// Web Application Firewall
    /// </summary>
    WAF = 1,

    /// <summary>
    /// Content Delivery Network
    /// </summary>
    CDN = 2,

    /// <summary>
    /// Both WAF and CDN
    /// </summary>
    Both = WAF | CDN
}
