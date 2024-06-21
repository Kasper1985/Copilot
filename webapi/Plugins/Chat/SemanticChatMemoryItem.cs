using System.Text.Json.Serialization;

namespace WebApi.Plugins.Chat;

/// <summary>
/// A single entry in the chat memory.
/// </summary>
public class SemanticChatMemoryItem(string label, string details)
{
    /// <summary>
    /// Label for the chat memory item.
    /// </summary>
    [JsonPropertyName("label")] public string Label { get; set; } = label;
    
    /// <summary>
    /// Details for the chat memory item.
    /// </summary>
    [JsonPropertyName("details")] public string Details { get; set; } = details;
    
    public string ToFormattedString() =>  $"{Label}: {Details.Trim()}";
}
