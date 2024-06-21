using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using WebApi.Enums;

namespace WebApi.Models.Response;

/// <summary>
/// Chat archive embedding configuration.
/// </summary>
public class ChatArchiveEmbeddingConfig
{
    /// <summary>
    /// The AI service.
    /// </summary>
    [Required]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public AIServiceType AIService { get; set; } = AIServiceType.AzureOpenAIEmbedding;
    
    /// <summary>
    /// The deployment or the model id.
    /// </summary>
    public string DeploymentOrModelId { get; set; } = string.Empty;
}
