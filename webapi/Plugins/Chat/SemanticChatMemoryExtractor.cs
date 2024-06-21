using System.Globalization;
using Microsoft.KernelMemory;
using Microsoft.SemanticKernel;
using WebApi.Enums;
using WebApi.Extensions;
using WebApi.Options;
using WebApi.Plugins.Utils;

namespace WebApi.Plugins.Chat;

/// <summary>
/// Helper class to extract and create kernel memory from chat history.
/// </summary>
public static class SemanticChatMemoryExtractor
{
    /// <summary>
    /// Extract and save kernel memory.
    /// </summary>
    public static async Task ExtractSemanticChatMemory(string chatId, IKernelMemory memoryClient, Kernel kernel, KernelArguments kernelArguments, PromptsOptions options, ILogger logger,
        CancellationToken cancellationToken = default)
    {
        foreach (var memoryType in Enum.GetNames(typeof(SemanticMemoryType)))
        {
            try
            {
                if (!options.TryGetMemoryContainerName(memoryType, out var memoryName))
                {
                    logger.LogInformation("Unable to extract kernel memory for invalid memory type {memoryType}. Continuing...", memoryType);
                    continue;
                }
                var semanticMemory = await ExtractCognitiveMemory(memoryType, memoryName, options, kernelArguments, kernel, logger, cancellationToken);
                foreach (var item in semanticMemory.Items)
                    await CreateMemory(memoryName, item.ToFormattedString(), chatId, memoryClient, options, logger, cancellationToken);
            }
            catch (Exception ex) when (!ex.IsCriticalException())
            {
                // Skip kernel memory extraction for this item if it fails.
                // We cannot rely on the model to response with perfect Json each time.
                logger.LogInformation("Unable to extract kernel memory for {memoryType}: {ex.Message}. Continuing...", memoryType, ex.Message);
            }
        }
    }
    
    /// <summary>
    /// Extracts the semantic chat memory from the chat session.
    /// </summary>
    private static async Task<SemanticChatMemory> ExtractCognitiveMemory(string memoryType, string memoryName, PromptsOptions options, KernelArguments kernelArguments, Kernel kernel, ILogger logger,
        CancellationToken cancellationToken = default)
    {
        if (!options.MemoryMap.TryGetValue(memoryName, out var memoryPrompt))
            throw new ArgumentException($"Memory name {memoryName} is not supported");
        
        // Token limit for chat history
        var tokenLimit = options.CompletionTokenLimit;
        var remainingTokens = tokenLimit - options.ResponseTokenLimit - TokenUtils.TokenCount(memoryPrompt);

        var memoryExtractionArguments = new KernelArguments(kernelArguments)
        {
            ["tokenLimit"] = remainingTokens.ToString(new NumberFormatInfo()),
            ["memoryName"] = memoryName,
            ["format"] = options.MemoryFormat,
            ["knowledgeCutoff"] = options.KnowledgeCutoffDate
        };
        
        var completionFunction = kernel.CreateFunctionFromPrompt(memoryPrompt);
        var result = await completionFunction.InvokeAsync(kernel, memoryExtractionArguments, cancellationToken);
        
        // Get token usage from ChatCompletion result and add to context
        var tokenUsage = TokenUtils.GetFunctionTokenUsage(result, logger);
        if (tokenUsage is not null)
            // Since there are multiple memory types, total token usage is calculated by cumulating the token usage of each memory type.
            kernelArguments[TokenUtils.GetFunctionKey($"SystemCognitive_{memoryType}")] = tokenUsage;
        else
            logger.LogError("Unable to determine token usage for {memoryType}", $"SystemCognitive_{memoryType}");

        return SemanticChatMemory.FromJson(result.ToString());
    }
    
    /// <summary>
    /// Create a memory item in the memory collection.
    /// If there is already a memory item that has a high similarity score with the new item, it will be skipped.
    /// </summary>
    private static async Task CreateMemory(string memoryName, string memory, string chatId, IKernelMemory memoryClient, PromptsOptions options, ILogger logger,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Search if there is already a memory item that has a high similarity score with the new item.
            var searchResult = await memoryClient.SearchMemory(options.MemoryIndexName, memory, options.SemanticMemoryRelevanceUpper, resultCount: 1, chatId, memoryName, cancellationToken);
            if (searchResult.Results.Count == 0)
                await memoryClient.StoreMemory(options.MemoryIndexName, chatId, memoryName, memory, cancellationToken);
        }
        catch (Exception ex) when (!ex.IsCriticalException())
        {
            // A store exception might be thrown if the collection does not exist, depending on hte memory store connector.
            logger.LogError(ex, "Unexpected failure searching {MemoryIndexName}", options.MemoryIndexName);
        }
    }
}
