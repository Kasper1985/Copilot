using System.Text.Json.Serialization;
using WebApi.Models.Storage;

namespace WebApi.Models.Response;

/// <summary>
/// Response object definition to the 'chats' POST request.
/// This groups the initial bot message with the chat session
/// to avoid making two requests.
/// </summary>
public class CreateChatResponse(ChatSession chatSession, CopilotChatMessage initialBotMessage)
{
    /// <summary>
    /// The chat session that was created.
    /// </summary>
    [JsonPropertyName("chatSession")]
    public ChatSession ChatSession { get; set; } = chatSession;

    /// <summary>
    /// Initial bot message.
    /// </summary>
    [JsonPropertyName("initialBotMessage")]
    public CopilotChatMessage InitialBotMessage { get; set; } = initialBotMessage;
}
