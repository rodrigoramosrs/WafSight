namespace WafSight.Models;

/// <summary>
/// Method used to identify a WAF/CDN
/// </summary>
public enum DetectionMethod
{
    /// <summary>
    /// Detection via HTTP headers
    /// </summary>
    Header,

    /// <summary>
    /// Detection via response body
    /// </summary>
    Body,

    /// <summary>
    /// Detection via HTTP status code
    /// </summary>
    StatusCode,

    /// <summary>
    /// Detection via DNS (CNAME records)
    /// </summary>
    DNS,

    /// <summary>
    /// Detection via timing analysis
    /// </summary>
    Timing,

    /// <summary>
    /// Detection via TLS certificate
    /// </summary>
    Certificate,

    /// <summary>
    /// Detection via cookies
    /// </summary>
    Cookie,

    /// <summary>
    /// Detection via payload probing
    /// </summary>
    Payload
}
