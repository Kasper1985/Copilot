using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.KernelMemory;
using WebApi.Auth;
using WebApi.Auth.Interfaces;
using WebApi.Models.Request;
using WebApi.Models.Response;
using WebApi.Models.Storage;
using WebApi.Options;
using WebApi.Plugins.Utils;
using WebApi.Storage.Repositories;

namespace WebApi.Controllers;

/// <summary>
/// Controller for chat history.
/// This controller is responsible for creating new chat sessions, retrieving chat sessions,
/// retrieving chat messages, and editing chat sessions.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("chats")]
public class ChatHistoryController(ILogger<ChatHistoryController> logger, IKernelMemory memoryClient, ChatSessionRepository sessionRepository, CopilotChatMessageRepository messageRepository,
    ChatParticipantRepository participantRepository, ChatMemorySourceRepository sourceRepository, IOptions<PromptsOptions> promptsOptions) : ControllerBase
{
    private readonly PromptsOptions _promptsOptions = promptsOptions.Value;

    [HttpGet("{chatId:guid}", Name = nameof(GetChat))]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetChat(Guid chatId)
    {
        ChatSession? chat = null;
        if (await sessionRepository.TryFindById(chatId.ToString(), callback: v => chat = v))
            return Ok(chat);
        
        return NotFound($"No chat session found for chat ID '{chatId}'.");
    }
    
    /// <summary>
    /// Create a new chat session and populate the session with the initial bot message.
    /// </summary>
    /// <param name="chatParameters">Contains the title of the chat.</param>
    /// <returns>The HTTP action result.</returns>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateChatSessionAsync([FromBody] CreateChatParameters chatParameters)
    {
        if (string.IsNullOrWhiteSpace(chatParameters.Title))
            return BadRequest("Chat session parameters cannot be null.");

        // Create a new chat session
        var newChat = new ChatSession(chatParameters.Title, _promptsOptions.SystemDescription);
        await sessionRepository.Create(newChat);

        // Create initial bot message
        var chatMessage = CopilotChatMessage.CreateBotResponseMessage(
            newChat.Id,
            _promptsOptions.InitialBotMessage,
            string.Empty, // The initial bot message doesn't need a prompt.
            null,
            TokenUtils.EmptyTokenUsages());
        await messageRepository.Create(chatMessage);
        
        var authInfo = new AuthInfo();

        // Add the user to the chat session
        await participantRepository.Create(new ChatParticipant(authInfo.UserId, newChat.Id));

        logger.LogDebug("Created chat session with id {chatId}.", newChat.Id);

        return CreatedAtRoute(nameof(GetChat), new { chatId = newChat.Id }, new CreateChatResponse(newChat, chatMessage));
    }
}
