using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using WebApi.Auth;
using WebApi.Auth.Interfaces;
using WebApi.Hubs;
using WebApi.Models.Request;
using WebApi.Models.Response;
using WebApi.Models.Storage;
using WebApi.Options;
using WebApi.Plugins.Chat;
using WebApi.Storage.Repositories;

namespace WebApi.Controllers;

/// <summary>
/// Controller responsible for chat messages and responses.
/// </summary>
[ApiController]
[AllowAnonymous]
public class ChatController(ILogger<ChatController> logger, IHttpClientFactory httpClientFactory, IOptions<ServiceOptions> serviceOptions, IOptions<PromptsOptions> promptOptions)
    : ControllerBase, IDisposable
{
    private readonly ServiceOptions _serviceOptions = serviceOptions.Value;
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly IOptions<PromptsOptions> _promptOptions = promptOptions;
    private readonly List<IDisposable> _disposables = [];
    
    private const string ChatPluginName = nameof(ChatPlugin);
    private const string ChatFunctionName = "Chat";
    private const string GeneratingResponseClientCall = "ReceiveBotREsponseStatus";

    [HttpPost("chats/{chatId:guid}/messages")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status504GatewayTimeout)]
    public async Task<IActionResult> Chat(
        [FromServices] Kernel kernel, 
        [FromServices] IHubContext<MessageRelayHub> messageRelayHubContext,
        [FromServices] ChatSessionRepository chatSessionRepository,
        [FromServices] ChatParticipantRepository chatParticipantRepository,
        [FromBody] Ask ask,
        [FromRoute] Guid chatId)
    {
        logger.LogDebug("Chat message received.");
        var chatIdString = chatId.ToString();
        
        // Only for tests purposes
        var authInfo = new AuthInfo();
        
        // Put ask's variables in the context we will use.
        var contextVariables = GetContextVariables(ask, authInfo, chatIdString);
        
        // Verify that the chat exists and that the user has access to it.
        ChatSession? chat = null;
        if (!await chatSessionRepository.TryFindById(chatIdString, callback: c => chat = c))
            return NotFound($"Failed to find chat session for the chat with ID '{chatIdString}' specified in variables.");
        
        if (!await chatParticipantRepository.IsUserInChat(authInfo.UserId, chatIdString))
            return Forbid($"User does not have access to the chat with ID '{chatIdString}' specified in variables.");
        
        // Get kernel function for generating response
        var chatFunction = kernel.Plugins.GetFunction(ChatPluginName, ChatFunctionName);
        FunctionResult? result = null;
        try
        {
            using var cts = _serviceOptions.TimeoutLimitInS is not null
                ? new CancellationTokenSource(TimeSpan.FromSeconds((double)_serviceOptions.TimeoutLimitInS))
                : null;
            
            result = await kernel.InvokeAsync(chatFunction, contextVariables, cts?.Token ?? default);
        }
        catch (Exception ex)
        {
            if (ex is not OperationCanceledException && ex.InnerException is not OperationCanceledException) throw;
            // Log the timeout and return a 504 response
            logger.LogError("The {FunctionName} operation timed out.", ChatFunctionName);
            return StatusCode(StatusCodes.Status504GatewayTimeout, $"The chat {ChatFunctionName} timed out.");
        }

        var chatAskResult = new AskResult
        {
            Value = result?.ToString() ?? string.Empty,
            Variables = contextVariables.Select(v => new KeyValuePair<string, object?>(v.Key, v.Value))
        };
        
        // Broadcast the response to all chat participants
        await messageRelayHubContext.Clients.Group(chatIdString).SendAsync(GeneratingResponseClientCall, chatAskResult, null);
        return Ok(chatAskResult);
    }
    
    /// <summary>
    /// Dispose of the object.
    /// </summary>
    protected virtual void Dispose(bool disposing)
    {
        if (!disposing) return;
        foreach (var disposable in _disposables)
            disposable.Dispose();
    }
    
    /// <inheritdoc/>
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
    
    private static KernelArguments GetContextVariables(Ask ask, IAuthInfo authInfo, string chatId)
    {
        const string userIdKey = "userId";
        const string userNameKey = "userName";
        const string chatIdKey = "chatId";
        const string messageKey = "message";
        
        var contextVariables = new KernelArguments();
        foreach (var variable in ask.Variables)
            contextVariables[variable.Key] = variable.Value;
        
        contextVariables[userIdKey] = authInfo.UserId;
        contextVariables[userNameKey] = authInfo.Name;
        contextVariables[chatIdKey] = chatId;
        contextVariables[messageKey] = ask.Input;
        
        return contextVariables;
    }
}
