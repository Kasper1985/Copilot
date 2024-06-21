using System.Text.Json.Serialization;

namespace WebApi.Models.Response;

/// <summary>
/// Response definition to image content safety analysis requests.
/// Endpoint made by the AzureContentSafety.
/// </summary>
public class ImageAnalysisResponse
{
    /// <summary>
    /// Gets or sets the <see cref="AnalysisResult"/> related to hate.
    /// </summary>
    [JsonPropertyName("hateResult")]
    public AnalysisResult? HateResult { get; set; }
    
    /// <summary>
    /// Gets or sets the <see cref="AnalysisResult"/> related to self-harm.
    /// </summary>
    [JsonPropertyName("selfHarmResult")]
    public AnalysisResult? SelfHarmResult { get; set; }
    
    /// <summary>
    /// Gets or sets the <see cref="AnalysisResult"/> related to sexual content.
    /// </summary>
    [JsonPropertyName("sexualResult")]
    public AnalysisResult? SexualResult { get; set; }
    
    /// <summary>
    /// Gets or sets the <see cref="AnalysisResult"/> related to violence.
    /// </summary>
    [JsonPropertyName("violenceResult")]
    public AnalysisResult? ViolenceResult { get; set; }
}
