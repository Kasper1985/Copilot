using System.Text.Json;
using System.Text.Json.Serialization;

namespace WebApi.Plugins.Chat;

/// <summary>
/// A collection of semantic chat memory.
/// </summary>
public class SemanticChatMemory
{
    /// <summary>
    /// The chat memory items.
    /// </summary>
    [JsonPropertyName("items")] public List<SemanticChatMemoryItem> Items { get; set; } = [];
    
    /// <summary>
    /// Create and add a chat memory item.
    /// </summary>
    /// <param name="label">Label for the chat memory item.</param>
    /// <param name="details">Details for the chat memory item.</param>
    public void AddItem(string label, string details)
    {
        Items.Add(new SemanticChatMemoryItem(label, details));
    }
    
    /// <summary>
    /// Serialize the chat memory to a JSON string
    /// </summary>
    /// <returns>A JSON string representing the chat memory.</returns>
    public override string ToString() => JsonSerializer.Serialize(this);
    
    /// <summary>
    /// Create a <see cref="SemanticChatMemory"/> object from a JSON string.
    /// </summary>
    /// <param name="json">JSON string to deserialize.</param>
    /// <returns>A semantic chat memory.</returns>
    public static SemanticChatMemory FromJson(string json) => JsonSerializer.Deserialize<SemanticChatMemory>(json) ?? throw new ArgumentException("Failed to deserialize chat memory to json.");
}
