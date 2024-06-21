using System.ComponentModel.DataAnnotations;
using WebApi.Attributes;

namespace WebApi.Options;

/// <summary>
/// Configuration options for content safety.
/// </summary>
public class ContentSafetyOptions
{
    public const string PropertyName = "ContentSafety";
 
    /// <summary>
    /// Whether to enable content safety.
    /// </summary>
    [Required]
    public bool Enabled { get; set; } = false;
    
    /// <summary>
    /// Content Safety endpoint.
    /// </summary>
    [RequiredOnPropertyValue(nameof(Enabled), true)]
    public string Endpoint { get; set; } = string.Empty;
    
    /// <summary>
    /// Key to access the content safety service.
    /// </summary>
    [RequiredOnPropertyValue(nameof(Enabled), true)]
    public string Key { get; set; } = string.Empty;
    
    /// <summary>
    /// Set the violation threshold.
    /// </summary>
    [Range(0, 6)]
    public short ViolationThreshold { get; set; } = 4;
}
