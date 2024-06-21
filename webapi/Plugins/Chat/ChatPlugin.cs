using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using Microsoft.KernelMemory;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using WebApi.Auth;
using WebApi.Enums;
using WebApi.Hubs;
using WebApi.Models.Response;
using WebApi.Models.Storage;
using WebApi.Options;
using WebApi.Plugins.Utils;
using WebApi.Services;
using WebApi.Storage.Repositories;

namespace WebApi.Plugins.Chat;

/// <summary>
/// ChatPlugin offers a more coherent chat experience by using memories to extract
/// conversation history and user intentions.
/// </summary>
public class ChatPlugin
{
    /// <summary>
    /// A kernel instance to create a completion function since each invocation
    /// of the <see cref="Chat"/> function will generate a new prompt dynamically.
    /// </summary>
    private readonly Kernel _kernel;

    /// <summary>
    /// Client for the kernel memory service.
    /// </summary>
    private readonly IKernelMemory _memoryClient;

    /// <summary>
    /// A logger instance to log events.
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    /// A repository to save and retrieve chat messages.
    /// </summary>
    private readonly CopilotChatMessageRepository _chatMessageRepository;

    /// <summary>
    /// A repository to save and retrieve chat sessions.
    /// </summary>
    private readonly ChatSessionRepository _chatSessionRepository;

    /// <summary>
    /// A SignalR hub context to broadcast updates of the execution.
    /// </summary>
    private readonly IHubContext<MessageRelayHub> _messageRelayHubContext;

    /// <summary>
    /// Settings containing prompt texts.
    /// </summary>
    private readonly PromptsOptions _promptOptions;

    /// <summary>
    /// A kernel memory retriever instance to query semantic memories.
    /// </summary>
    private readonly SemanticMemoryRetriever _semanticMemoryRetriever;

    /// <summary>
    /// Azure content safety moderator.
    /// </summary>
    private readonly AzureContentSafety? _contentSafety = null;
    
    
    /// <summary>
    /// Creates a new instance of the <see cref="ChatPlugin"/> class.
    /// </summary>
    public ChatPlugin(Kernel kernel, IKernelMemory memoryClient, CopilotChatMessageRepository chatMessageRepository, ChatSessionRepository chatSessionRepository,
        IHubContext<MessageRelayHub> messageRelayHubContext, IOptions<PromptsOptions> promptOptions, IOptions<DocumentMemoryOptions> documentImportOptions, AzureContentSafety? contentSafety,
        ILogger logger)
    {
        _kernel = kernel;
        _memoryClient = memoryClient;
        _chatMessageRepository = chatMessageRepository;
        _chatSessionRepository = chatSessionRepository;
        _messageRelayHubContext = messageRelayHubContext;
        
        // Clone the prompt options to avoid modifying the original prompt options.
        _promptOptions = promptOptions.Value.Copy();

        _semanticMemoryRetriever = new SemanticMemoryRetriever(promptOptions, chatSessionRepository, memoryClient);
        _contentSafety = contentSafety;
        _logger = logger;
    }

    /// <summary>
    /// Method that wraps GetAllowedChatHistory to get allotted history messages as one string.
    /// GetAllowedChatHistory optionally updates a <see cref="ChatHistory"/> object with the allotted messages,
    /// but the ChatHistory type is not supported when calling from a rendered prompt, so this wrapper bypasses the chatHistory parameter.
    /// </summary>
    /// <param name="chatId">The chat ID.</param>
    /// <param name="tokenLimit">The maximum number of tokens.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns></returns>
    [KernelFunction, Description("Extract chat history")]
    public Task<string> ExtractChatHistory([Description("Chat ID to extract history from")] string chatId, [Description("Maximum number of tokens")] int tokenLimit,
        CancellationToken cancellationToken = default) =>
        GetAllowedChatHistory(chatId, tokenLimit, cancellationToken: cancellationToken);
    
    /// <summary>
    /// Extracts chat history within token limit as a formatted string and optionally update the <see cref="ChatHistory"/> object with the allotted messages.
    /// </summary>
    /// <param name="chatId">Chat ID of the chat to extract history from.</param>
    /// <param name="tokenLimit">Maximum number of tokens.</param>
    /// <param name="chatHistory">Optional <see cref="ChatHistory"/> object tracking allotted messages.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Chat history as a string.</returns>
    private async Task<string> GetAllowedChatHistory(string chatId, int tokenLimit, ChatHistory? chatHistory = null, CancellationToken cancellationToken = default)
    {
        var sortedMessages = await _chatMessageRepository.FindByChatId(chatId, 0, 100);

        var allottedChatHistory = new ChatHistory();
        var historyText = string.Empty;
        var remainingTokens = tokenLimit;

        foreach (var chatMessage in sortedMessages)
        {
            var formattedMessage = chatMessage.ToFormattedString();
            if (chatMessage.Type == ChatMessageType.Document) continue;
            
            var tokenCount = chatHistory is not null ? TokenUtils.GetContextMessagesTokenCount(chatHistory) : TokenUtils.TokenCount(formattedMessage);
            if (remainingTokens - tokenCount >= 0)
            {
                historyText = $"{formattedMessage}\n{historyText}";
                if (chatMessage.AuthorRole == AuthorRoles.Bot)
                    // Message doesn't have to be formatted for bot. This helps with asserting a natural language response from the LLM (no date or author preamble).
                    allottedChatHistory.AddAssistantMessage(chatMessage.Content.Trim());
                else
                {
                    // Omit username if Auth is disabled.
                    var userMessage = PassThroughAuthenticationHandler.IsDefaultUser(chatMessage.UserId)
                        ? $"[{chatMessage.Timestamp.ToString()}] {chatMessage.Content}"
                        : formattedMessage;
                    allottedChatHistory.AddUserMessage(userMessage.Trim());
                }

                remainingTokens -= tokenCount;
            }
            else
                break;
        }
        
        chatHistory?.AddRange(allottedChatHistory.Reverse());
        return $"Chat history:\n{historyText.Trim()}";
    }

    /// <summary>
    /// This is the entry point for getting a chat response. It manages the token limit, saves messages to memory,
    /// and fills in the necessary context variables for completing the prompt that will be rendered by the template engine.
    /// </summary>
    [KernelFunction, Description("Get chat response")]
    public async Task<KernelArguments> Chat(
        [Description("The new message")] string message,
        [Description("Unique and persistent identifier for the user")] string userId,
        [Description("Name of the user")] string userName,
        [Description("Unique and persistent identifier for the chat")] string chatId,
        [Description("Type of the message")] string messageType,
        KernelArguments context,
        CancellationToken cancellationToken = default)
    {
        // Set the system description in the prompt options
        await SetSystemDescription(chatId, cancellationToken);
        
        // Save this new message to memory such that subsequent chat responses can use it.
        await UpdateBotResponseStatusOnClient(chatId, "Saving user message to chat history", cancellationToken);
        var newUserMessage = await SaveNewMessage(message, userId, userName, chatId, messageType, cancellationToken);
        
        // Clone the context to avoid modifying the original context variables.
        var chatContext = new KernelArguments(context) { ["knowledgeCutoff"] = _promptOptions.KnowledgeCutoffDate };

        var chatMessage = await GetChatResponse(chatId, userId, chatContext, newUserMessage, cancellationToken);
        context["input"] = chatMessage.Content;
        
        if (chatMessage.TokenUsage is not null)
            context["tokenUsage"] = JsonSerializer.Serialize(chatMessage.TokenUsage);
        else
            _logger.LogWarning("{Method} token usage unknown. Ensure token management has been implemented correctly.", nameof(Chat));

        return context;
    }
    
    /// <summary>
    /// Sets the system description in the prompt options.
    /// </summary>
    /// <param name="chatId">ID of the chat session.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <exception cref="ArgumentException">Throws if the chat session does not exist.</exception>
    private async Task SetSystemDescription(string chatId, CancellationToken cancellationToken = default)
    {
        ChatSession? chatSession = null;
        if (!await _chatSessionRepository.TryFindById(chatId, callback: s => chatSession = s))
            throw new ArgumentException("Chat session does not exist.");

        _promptOptions.SystemDescription = chatSession!.SafeSystemDescription;
    }
    
    /// <summary>
    /// Update the status of the response on the client.
    /// </summary>
    /// <param name="chatId">The chat ID.</param>
    /// <param name="status">Current status of the response.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    private async Task UpdateBotResponseStatusOnClient(string chatId, string status, CancellationToken cancellationToken = default)
    {
        await _messageRelayHubContext.Clients.Group(chatId).SendAsync("BotStatus", status, cancellationToken);
    }
    
    /// <summary>
    /// Save a new message to the chat history.
    /// </summary>
    private async Task<CopilotChatMessage> SaveNewMessage(string message, string userId, string userName, string chatId, string type, CancellationToken cancellationToken = default)
    {
        // Make sure the chat exists.
        if (!await _chatSessionRepository.TryFindById(chatId))
            throw new ArgumentException("Chat session does not exist.");
        
        // Default to a standard message type if the 'type' is not recognized.
        var messageType = Enum.TryParse(type, out ChatMessageType typeAsEnum) && Enum.IsDefined(typeof(ChatMessageType), typeAsEnum) ? typeAsEnum : ChatMessageType.Message;
        var chatMessage = new CopilotChatMessage(userId, userName, chatId, message, string.Empty, null, AuthorRoles.User, messageType);

        await _chatMessageRepository.Create(chatMessage);
        return chatMessage;
    }
    
    /// <summary>
    /// Generate the necessary chat context to create a prompt then invoke the model to get a response.
    /// </summary>
    /// <returns>The created chat message containing the model-generated response.</returns>
    private async Task<CopilotChatMessage> GetChatResponse(string chatId, string userId, KernelArguments chatContext, CopilotChatMessage userMessage, CancellationToken cancellationToken = default)
    {
        // Render system instruction component and create the meta-prompt template.
        var systemInstructions = await AsyncUtils.SafeInvoke(() => RenderSystemInstructions(chatId, chatContext, cancellationToken), nameof(RenderSystemInstructions));
        var metaPrompt = new ChatHistory(systemInstructions);
        
        // Bypass audience extraction if Auth is disabled.
        var audience = string.Empty;
        if (!PassThroughAuthenticationHandler.IsDefaultUser(userId))
        {
            // Get the audience
            await UpdateBotResponseStatusOnClient(chatId, "Extracting audience", cancellationToken);
            audience = await AsyncUtils.SafeInvoke(() => GetAudience(chatContext, cancellationToken), nameof(GetAudience));
            metaPrompt.AddSystemMessage(audience);
        }
        
        // Extract user intent from the conversation history.
        await UpdateBotResponseStatusOnClient(chatId, "Extracting user intent", cancellationToken);
        var userIntent = await AsyncUtils.SafeInvoke(() => GetUserIntent(chatContext, cancellationToken), nameof(GetUserIntent));
        metaPrompt.AddSystemMessage(userIntent);
        
        // Calculate max amount of tokens to use for memories
        var maxRequestTokenBudget = GetMaxRequestTokenBudget();
        // Calculate tokens used so far: system instructions, audience extraction and user intent
        var tokenUsed = TokenUtils.GetContextMessagesTokenCount(metaPrompt);
        var chatMemoryTokenBudget = maxRequestTokenBudget - tokenUsed - TokenUtils.GetContextMessageTokenCount(AuthorRole.User, userMessage.ToFormattedString());
        chatMemoryTokenBudget = (int)(chatMemoryTokenBudget * _promptOptions.MemoriesResponseContextWeight);
        
        // Query relevant semantic and document memories
        await UpdateBotResponseStatusOnClient(chatId, "Extracting semantic and document memories", cancellationToken);
        var (memoryText, citationMap) = await _semanticMemoryRetriever.QueryMemories(userIntent, chatId, chatMemoryTokenBudget);
        if (!string.IsNullOrWhiteSpace(memoryText))
        {
            metaPrompt.AddSystemMessage(memoryText);
            tokenUsed += TokenUtils.GetContextMessageTokenCount(AuthorRole.System, memoryText);
        }
        
        // Add as many chat history messages to meta-prompt as the token budget allows
        await UpdateBotResponseStatusOnClient(chatId, "Extracting chat history", cancellationToken);
        var allowedChatHistory = await GetAllowedChatHistory(chatId, maxRequestTokenBudget - tokenUsed, metaPrompt, cancellationToken);
        
        // Store token usage of prompt template
        chatContext[TokenUtils.GetFunctionKey("SystemMetaPrompt")] = TokenUtils.GetContextMessagesTokenCount(metaPrompt).ToString(CultureInfo.CurrentCulture);
        
        // Stream the response to the client
        var promptView = new BotResponsePrompt(systemInstructions, audience, userIntent, memoryText, allowedChatHistory, metaPrompt);
        
        return await HandleBotResponse(chatId, userId, chatContext, promptView, citationMap.Values.AsEnumerable(), cancellationToken);
    }
    
    /// <summary>
    /// Helper function to render system instruction components.
    /// </summary>
    /// <param name="chatId">The chat ID.</param>
    /// <param name="context">The <see cref="KernelArguments"/> as chat context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    private async Task<string> RenderSystemInstructions(string chatId, KernelArguments context, CancellationToken cancellationToken = default)
    {
        // Render system instruction components
        await UpdateBotResponseStatusOnClient(chatId, "Initializing prompt", cancellationToken);

        var promptTemplateFactory = new KernelPromptTemplateFactory();
        var promptTemplate = promptTemplateFactory.Create(new PromptTemplateConfig(_promptOptions.SystemPersona));
        return await promptTemplate.RenderAsync(_kernel, context, cancellationToken);
    }

    /// <summary>
    /// Extract the list of participants from the conversation history.
    /// Note that only those who have spoken will be included.
    /// </summary>
    /// <param name="context"><see cref="KernelArguments"/> as context with variables.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns></returns>
    private async Task<string> GetAudience(KernelArguments context, CancellationToken cancellationToken = default)
    {
        // Clone the context to avoid modifying the original context variables.
        var audienceContext = new KernelArguments(context);
        var historyTokenBudget = _promptOptions.CompletionTokenLimit 
                                 - _promptOptions.ResponseTokenLimit 
                                 - TokenUtils.TokenCount(string.Join("\n\n", [_promptOptions.SystemAudience, _promptOptions.SystemAudienceContinuation]));
        
        audienceContext["tokenLimit"] = historyTokenBudget.ToString(new NumberFormatInfo());

        var completionFunction = _kernel.CreateFunctionFromPrompt(
            _promptOptions.SystemAudienceExtraction,
            CreateIntentCompletionSettings(),
            functionName: "SystemAudienceExtraction",
            description: "Extract audience");

        var result = await completionFunction.InvokeAsync(_kernel, audienceContext, cancellationToken);
        
        // Get token usage from ChatCompletion result and add to original context
        var tokenUsage = TokenUtils.GetFunctionTokenUsage(result, _logger);
        if (tokenUsage is not null)
            context[TokenUtils.GetFunctionKey("SystemAudienceExtraction")] = tokenUsage;
        else
            _logger.LogError("Unable to determine token usage for SystemAudienceExtraction function.");

        return $"List of participants: {result}";
    }

    /// <summary>
    /// Create <see cref="OpenAIPromptExecutionSettings"/> for intent response. Parameters are read from the <see cref="PromptsOptions"/> class.
    /// </summary>
    private OpenAIPromptExecutionSettings CreateIntentCompletionSettings() => new OpenAIPromptExecutionSettings
    {
        MaxTokens = _promptOptions.ResponseTokenLimit,
        Temperature = _promptOptions.IntentTemperature,
        TopP = _promptOptions.IntentTopP,
        FrequencyPenalty = _promptOptions.IntentFrequencyPenalty,
        PresencePenalty = _promptOptions.IntentPresencePenalty,
        StopSequences = ["] bot:"]
    };

    /// <summary>
    /// Extract user intent from the conversation history.
    /// </summary>
    /// <param name="context">Kernel context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns></returns>
    private async Task<string> GetUserIntent(KernelArguments context, CancellationToken cancellationToken = default)
    {
        // Clone the context to avoid modifying the original context variables.
        var intentContext = new KernelArguments(context);
        
        var tokenBudget = _promptOptions.CompletionTokenLimit
                            - _promptOptions.ResponseTokenLimit
                            - TokenUtils.TokenCount(string.Join("\n", [_promptOptions.SystemPersona, _promptOptions.SystemIntent, _promptOptions.SystemIntentContinuation]));
        intentContext["tokenLimit"] = tokenBudget.ToString(new NumberFormatInfo());
        intentContext["knowledgeCutoff"] = _promptOptions.KnowledgeCutoffDate;
        
        var completionFunction = _kernel.CreateFunctionFromPrompt(
            _promptOptions.SystemIntentExtraction,
            CreateIntentCompletionSettings(),
            functionName: "UserIntentExtraction",
            description: "Extract user intent");
        
        var result = await completionFunction.InvokeAsync(_kernel, intentContext, cancellationToken);
        
        // Get token usage from ChatCompletion result and add to original context
        var tokenUsage = TokenUtils.GetFunctionTokenUsage(result, _logger);
        if (tokenUsage is not null)
            context[TokenUtils.GetFunctionKey("SystemIntentExtraction")] = tokenUsage;
        else
            _logger.LogError("Unable to determine token usage for userIntentExtraction.");
        
        return $"User intent: {result}";
    }

    /// <summary>
    /// Calculate the maximum number of tokens that can be sent in a request.
    /// </summary>
    /// <returns></returns>
    private int GetMaxRequestTokenBudget()
    {
        // OpenAI inserts a message under the hood:
        // "content": "Assistant is a large language model.", "role": "system"
        // This burns just under 20 tokens which need to be accounted for.
        const int extraOpenAiMessageTokens = 20;
        return _promptOptions.CompletionTokenLimit // Total token limit
               - extraOpenAiMessageTokens
               - _promptOptions.ResponseTokenLimit // Token count reserved for model to generate a response
               - _promptOptions.FunctionCallingTokenLimit; // Buffer for tool calls
    }

    /// <summary>
    /// Helper function to handle final steps of bot response generation, including streaming to client, generating semantic text memory,
    /// calculating final token usages, and saving to chat history.
    /// </summary>
    /// <param name="chatId">The chat ID.</param>
    /// <param name="userId">The user ID.</param>
    /// <param name="chatContext">Chat context.</param>
    /// <param name="promptView">The prompt view.</param>
    /// <param name="citations">Citation sources.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    private async Task<CopilotChatMessage> HandleBotResponse(string chatId, string userId, KernelArguments chatContext, BotResponsePrompt promptView, IEnumerable<CitationSource>? citations,
        CancellationToken cancellationToken = default)
    {
        // Get bot response and stream to client
        await UpdateBotResponseStatusOnClient(chatId, "Generating bot response", cancellationToken);
        var chatMessage = await AsyncUtils.SafeInvoke(() => StreamResponseToClient(chatId, userId, promptView, citations, cancellationToken), nameof(StreamResponseToClient));
        
        // Save message into chat history
        await UpdateBotResponseStatusOnClient(chatId, "Saving message to chat history", cancellationToken);
        await _chatMessageRepository.Upsert(chatMessage);
        
        // Extract semantic chat memory
        await UpdateBotResponseStatusOnClient(chatId, "Generating semantic chat memory", cancellationToken);
        await AsyncUtils.SafeInvoke(() => SemanticChatMemoryExtractor.ExtractSemanticChatMemory(chatId, _memoryClient, _kernel, chatContext, _promptOptions, _logger, cancellationToken),
            nameof(SemanticChatMemoryExtractor.ExtractSemanticChatMemory));
        
        // Calculate token usage for dependency functions and prompt template
        await UpdateBotResponseStatusOnClient(chatId, "Saving token usage", cancellationToken);
        chatMessage.TokenUsage = GetTokenUsages(chatContext, chatMessage.Content);
        
        // Update the message on client and in chat history with final completion token usage
        await UpdateMessageOnClient(chatMessage, cancellationToken);
        await _chatMessageRepository.Upsert(chatMessage);

        return chatMessage;
    }

    /// <summary>
    /// Stream the response to the client.
    /// </summary>
    /// <param name="chatId">The chat ID.</param>
    /// <param name="userId">The user ID.</param>
    /// <param name="prompt">Prompt used to generate the response.</param>
    /// <param name="citations">Citations for the message.</param>
    /// <param name="cancellationToken">The cancellation token-</param>
    /// <returns>The created chat message.</returns>
    private async Task<CopilotChatMessage> StreamResponseToClient(string chatId, string userId, BotResponsePrompt prompt, IEnumerable<CitationSource>? citations = null,
        CancellationToken cancellationToken = default)
    {
        // Create the stream
        var chatCompletion = _kernel.GetRequiredService<IChatCompletionService>();
        var stream = chatCompletion.GetStreamingChatMessageContentsAsync(prompt.MetaPromptTemplate, CreateChatRequestSettings(), _kernel, cancellationToken);
        
        // Create message on client
        var chatMessage = await CreateBotMessageOnClient(chatId, userId, JsonSerializer.Serialize(prompt), string.Empty, citations, cancellationToken: cancellationToken);
        
        // Stream the response to the client
        await foreach (var contentPiece in stream)
        {
            chatMessage.Content += contentPiece;
            await UpdateMessageOnClient(chatMessage, cancellationToken);
        }

        return chatMessage;
    }
    
    /// <summary>
    /// Create <see cref="OpenAIPromptExecutionSettings"/> for chat response. Parameters are read from the <see cref="PromptsOptions"/> class.
    /// </summary>
    private OpenAIPromptExecutionSettings CreateChatRequestSettings() => new()
    {
        MaxTokens = _promptOptions.ResponseTokenLimit,
        Temperature = _promptOptions.ResponseTemperature,
        TopP = _promptOptions.ResponseTopP,
        FrequencyPenalty = _promptOptions.ResponseFrequencyPenalty,
        PresencePenalty = _promptOptions.ResponsePresencePenalty,
        ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions
    };
    
    /// <summary>
    /// Create an empty message on the client to begin the response.
    /// </summary>
    /// <param name="chatId">The chat ID.</param>
    /// <param name="userId">The user ID.</param>
    /// <param name="prompt">Prompt used to generate the message.</param>
    /// <param name="content">Content of the message.</param>
    /// <param name="citations">Citations for the message.</param>
    /// <param name="tokenUsage">Total token usage of response completion.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The created chat message.</returns>
    private async Task<CopilotChatMessage> CreateBotMessageOnClient(string chatId, string userId, string prompt, string content, IEnumerable<CitationSource>? citations = null,
        Dictionary<string, int>? tokenUsage = null, CancellationToken cancellationToken = default)
    {
        var chatMessage = CopilotChatMessage.CreateBotResponseMessage(chatId, content, prompt, citations, tokenUsage);
        await _messageRelayHubContext.Clients.Group(chatId).SendAsync("ReceiveMessage", chatId, userId, chatMessage, cancellationToken);
        return chatMessage;
    }
    
    /// <summary>
    /// Update the response on the client.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    private async Task UpdateMessageOnClient(CopilotChatMessage message, CancellationToken cancellationToken = default)
    {
        await _messageRelayHubContext.Clients.Group(message.ChatId).SendAsync("ReceiveMessageUpdate", message, cancellationToken);
    }

    /// <summary>
    /// Gets token usage totals for each semantic function if not undefined.
    /// </summary>
    /// <param name="kernelArguments">Context maintained during response generation.</param>
    /// <param name="content">String representing bot response. If null, response is still being generated or was hardcoded.</param>
    /// <returns>Dictionary containing function to token usage mapping for each total that's defined.</returns>
    private static Dictionary<string, int> GetTokenUsages(KernelArguments kernelArguments, string? content = null)
    {
        var tokenUsageDict = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        
        // Total token usage of each semantic function
        foreach (var function in TokenUtils.SemanticFunctions.Values)
        {
            if (!kernelArguments.TryGetValue($"{function}TokenUsage", out var tokenUsage)) continue;
            if (tokenUsage is string tokenUsageString)
                tokenUsageDict.Add(function, int.Parse(tokenUsageString, CultureInfo.InvariantCulture));
        }
        
        if (content is not null)
            tokenUsageDict.Add(TokenUtils.SemanticFunctions["SystemCompletion"], TokenUtils.TokenCount(content));

        return tokenUsageDict;
    }
}
