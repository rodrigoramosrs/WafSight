namespace WafSight.Models;

/// <summary>
/// Detection context with collected information
/// </summary>
public class DetectionContext
{
    /// <summary>
    /// URL being analyzed
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Original HTTP response
    /// </summary>
    public HttpResponseData? Response { get; set; }

    /// <summary>
    /// DNS information
    /// </summary>
    public DnsInfo? DnsInfo { get; set; }

    /// <summary>
    /// User-Agent used
    /// </summary>
    public string UserAgent { get; set; } = "WafSight/2.0";

    /// <summary>
    /// Test payload responses
    /// </summary>
    public Dictionary<string, HttpResponseData> PayloadResponses { get; set; } = new();
}

/// <summary>
/// HTTP response data
/// </summary>
public class HttpResponseData
{
    public int StatusCode { get; set; }
    public Dictionary<string, string> Headers { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public string Body { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public TimeSpan ResponseTime { get; set; }
}

/// <summary>
/// DNS information
/// </summary>
public class DnsInfo
{
    public List<string> Cnames { get; set; } = new();
    public List<string> ARecords { get; set; } = new();
    public List<string> NsRecords { get; set; } = new();
    public List<string> TxtRecords { get; set; } = new();
}
