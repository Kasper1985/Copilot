using System.Text.Json.Serialization;
using WebApi.Enums;
using WebApi.Storage.Interfaces;

namespace WebApi.Models.Storage;

/// <summary>
/// The external memory source
/// </summary>
public class MemorySource(string chatId, string name, string sharedBy, MemorySourceType type, long size, Uri? hyperLink) : IStorageEntity
{
    /// <summary>
    /// Source ID that is persistent and unique.
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;
    
    /// <summary>
    /// The Chat ID.
    /// </summary>
    [JsonPropertyName("chatId")]
    public string ChatId { get; set; } = chatId;
    
    /// <summary>
    /// The type of the source.
    /// </summary>
    [JsonPropertyName("sourceType")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public MemorySourceType SourceType { get; set; } = type;
    
    /// <summary>
    /// The name of the source.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = name;
    
    /// <summary>
    /// The external link to hte source.
    /// </summary>
    [JsonPropertyName("hyperLink")]
    public Uri? HyperLink { get; set; } = hyperLink;
    
    /// <summary>
    /// The user ID of who shared the source.
    /// </summary>
    [JsonPropertyName("sharedBy")]
    public string SharedBy { get; set; } = sharedBy;
    
    /// <summary>
    /// When the source is created in the bot.
    /// </summary>
    [JsonPropertyName("createdOn")]
    public DateTimeOffset CreatedOn { get; set; }

    /// <summary>
    /// The size of the source in bytes.
    /// </summary>
    [JsonPropertyName("size")]
    public long Size { get; set; } = size;
    
    /// <summary>
    /// The number of tokens in the source.
    /// </summary>
    [JsonPropertyName("tokens")]
    public long Tokens { get; set; }
    
    /// <summary>
    /// THe partition key for the source
    /// </summary>
    [JsonIgnore]
    public string Partition => ChatId;
    
    /// <summary>
    /// Empty constructor for serialization.
    /// </summary>
    public MemorySource() : this(string.Empty, string.Empty, string.Empty, MemorySourceType.File, 0, null)
    {}
}
