using System.Text.Json;
using System.Text.Json.Serialization;
using WebApi.Models.Request;

namespace WebApi.Models.Response;

/// <summary>
/// Value of 'Content' for a 'ChatMessage' of type 'ChatMessageType.Document'.
/// </summary>
public class DocumentMessageContent
{
    private static readonly JsonSerializerOptions SerializerSettings = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    
    /// <summary>
    /// List of documents contained in the message.
    /// </summary>
    [JsonPropertyName("documents")]
    public IEnumerable<DocumentData> Documents { get; set; } = [];
    
    /// <summary>
    /// Add a document to the list of documents.
    /// </summary>
    /// <param name="name">Name of the uploaded document.</param>
    /// <param name="size">Size of the uploaded document.</param>
    /// <param name="isUploaded">Status of the uploaded document.</param>
    public void AddDocument(string name, string size, bool isUploaded)
    {
        Documents = Documents.Append(new DocumentData
        {
            Name = name,
            Size = size,
            IsUploaded = isUploaded
        });
    }
    
    /// <summary>
    /// Serialize the object to a JSON string.
    /// </summary>
    /// <returns>A serialized JSON string.</returns>
    public override string ToString() => JsonSerializer.Serialize(this, SerializerSettings);

    /// <summary>
    /// Serialize the object to a formatted string.
    /// Only successful uploads will be included in the formatted string.
    /// </summary>
    /// <returns>A formatted string with information about uploaded documents.</returns>
    public string ToFormattedString() => 
        !Documents.Any() ? string.Empty : string.Join(", ", Documents.Where(d => d.IsUploaded).Select(d => $"{d.Name} ({d.Size} Bytes)"));
    
    /// <summary>
    /// Serialize the object to a formatted string that only contains document names separated by comma.
    /// Only successful uploads will be included in the formatted string.
    /// </summary>
    /// <returns>A formatted string.</returns>
    public string ToFormattedStringNamesOnly() => 
        !Documents.Any() ? string.Empty : string.Join(", ", Documents.Where(d => d.IsUploaded).Select(d => d.Name));
    
    /// <summary>
    /// Deserialize a JSON string to a <see cref="DocumentMessageContent"/> object.
    /// </summary>
    /// <param name="json">A JSON string with serialized data of the object.</param>
    /// <returns>A <see cref="DocumentMessageContent"/> object.</returns>
    public static DocumentMessageContent? FromString(string json) => JsonSerializer.Deserialize<DocumentMessageContent>(json, SerializerSettings);
}
