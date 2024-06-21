using System.Text.Json.Serialization;
using Microsoft.SemanticKernel.ChatCompletion;

namespace WebApi.Models.Response;

/// <summary>
/// The final prompt sent to generate bot response.
/// </summary>
public class BotResponsePrompt(string systemPersona, string audience, string userIntent, string pastMemories, string chatHistory, ChatHistory metaPromptTemplate)
{
    /// <summary>
    /// The system persona of the chat, includes SystemDescription and SystemResponse components from PromptsOptions.cs.
    /// </summary>
    [JsonPropertyName("systemPersona")]
    public string SystemPersona { get; set; } = systemPersona;
    
    /// <summary>
    /// Audience extracted from conversation history.
    /// </summary>
    [JsonPropertyName("audience")]
    public string Audience { get; set; } = audience;
    
    /// <summary>
    /// User intent extracted from input and conversation history.
    /// </summary>
    [JsonPropertyName("userIntent")]
    public string UserIntent { get; set; } = userIntent;
    
    /// <summary>
    /// Chat memories queried from the chat memory store if any, includes long term and working memory
    /// </summary>
    [JsonPropertyName("chatMemories")]
    public string PastMemories { get; set; } = pastMemories;
    
    /// <summary>
    /// Most recent messages from chat history.
    /// </summary>
    [JsonPropertyName("chatHistory")]
    public string ChatHistory { get; set; } = chatHistory;
    
    /// <summary>
    /// The collection of context messages associated with this chat completions request.
    /// </summary>
    [JsonPropertyName("metaPromptTemplate")]
    public ChatHistory MetaPromptTemplate { get; set; } = metaPromptTemplate;
}
