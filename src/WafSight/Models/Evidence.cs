namespace WafSight.Models;

/// <summary>
/// WAF/CDN detection evidence
/// </summary>
public class Evidence
{
    /// <summary>
    /// Detection method used
    /// </summary>
    public DetectionMethod Method { get; set; }

    /// <summary>
    /// Header name, cookie, or evidence type
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Detected value
    /// </summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// Matching signature
    /// </summary>
    public string Signature { get; set; } = string.Empty;

    /// <summary>
    /// Confidence level for this evidence (0.0 to 1.0)
    /// </summary>
    public double Confidence { get; set; }

    /// <summary>
    /// Evidence description
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Weight of the evidence in the final calculation
    /// </summary>
    public double Weight { get; set; } = 1.0;
}
