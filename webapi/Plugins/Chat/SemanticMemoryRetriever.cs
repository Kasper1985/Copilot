using System.ComponentModel;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.KernelMemory;
using WebApi.Extensions;
using WebApi.Models.Storage;
using WebApi.Options;
using WebApi.Plugins.Utils;
using WebApi.Storage.Repositories;

namespace WebApi.Plugins.Chat;

/// <summary>
/// This class provided the function to query kernel memory.
/// </summary>
public class SemanticMemoryRetriever
{
    private readonly PromptsOptions _promptOptions;
    private readonly ChatSessionRepository _chatSessionRepository;
    private readonly IKernelMemory _memoryClient;
    private readonly List<string> _memoryNames;
    
    /// <summary>
    /// Creates a new instance of the <see cref="SemanticMemoryRetriever"/> class.
    /// </summary>
    public SemanticMemoryRetriever(IOptions<PromptsOptions> promptOptions, ChatSessionRepository chatSessionRepository, IKernelMemory memoryClient)
    {
        _promptOptions = promptOptions.Value;
        _chatSessionRepository = chatSessionRepository;
        _memoryClient = memoryClient;
        _memoryNames = new List<string>();

        _memoryNames =
        [
            _promptOptions.DocumentMemoryName,
            _promptOptions.LongTermMemoryName,
            _promptOptions.WorkingMemoryName
        ];
    }

    /// <summary>
    /// Query relevant memories base on the query.
    /// </summary>
    /// <returns>A string containing the relevant memories.</returns>
    public async Task<(string, IDictionary<string, CitationSource>)> QueryMemories(
        [Description("Query to match.")] string query,
        [Description("Chat ID to query history from")] string chatId,
        [Description("Maximum number of tokens")] int tokenLimit)
    {
        ChatSession? chatSession = null;
        if (!await _chatSessionRepository.TryFindById(chatId, callback: s => chatSession = s))
            throw new ArgumentException($"Chat session with ID {chatId} not found.");
        
        // Search for relevant memories.
        List<(Citation Citation, Citation.Partition Memory)> relevantMemories = [];
        List<Task> tasks = [];
        tasks.AddRange(_memoryNames.Select(memoryName => SearchMemory(memoryName, query, chatSession!, chatId, relevantMemories)));
        
        // Search for global document memory.
        tasks.Add(SearchMemory(_promptOptions.DocumentMemoryName, query, chatSession!, chatId, relevantMemories, isGlobalMemory: true));

        await Task.WhenAll(tasks);
        
        if (relevantMemories.Count == 0)
            return (string.Empty, new Dictionary<string, CitationSource>(StringComparer.OrdinalIgnoreCase));

        var builderMemory = new StringBuilder();
        var (memoryMap, citationMap) = ProcessMemories(relevantMemories, tokenLimit);
        FormatMemories(memoryMap, builderMemory);
        FormatSnippets(memoryMap, builderMemory);
        
        return (builderMemory.Length == 0 ? string.Empty : builderMemory.ToString(), citationMap);
    }
    
    /// <summary>
    /// Search the memory for relevant memories by memory name
    /// </summary>
    private async Task SearchMemory(string memoryName, string query, ChatSession chatSession, string chatId,
        List<(Citation Citation, Citation.Partition Memory)> relevantMemories, bool isGlobalMemory = false)
    {
        var searchResult = await _memoryClient.SearchMemory(
            _promptOptions.MemoryIndexName,
            query,
            CalculateRelevanceThreshold(memoryName, chatSession.MemoryBalance),
            isGlobalMemory ? DocumentMemoryOptions.GlobalDocumentChatId.ToString() : chatId,
            memoryName);

        relevantMemories.AddRange(searchResult.Results.SelectMany(c => c.Partitions.Select(p => (c, p))).ToList());
    }

    /// <summary>
    /// Process the relevant memories and return a map of memories with citations for each memory name.
    /// </summary>
    /// <returns>A map of memories for each memory name and a map of citations for documents.</returns>
    private (IDictionary<string, List<(string, CitationSource)>>, IDictionary<string, CitationSource>) ProcessMemories(List<(Citation Citation, Citation.Partition Memory)> relevantMemories,
        int remainingTokens)
    {
        var memoryMap = new Dictionary<string, List<(string, CitationSource)>>(StringComparer.OrdinalIgnoreCase);
        var citationMap = new Dictionary<string, CitationSource>(StringComparer.OrdinalIgnoreCase);

        foreach (var result in relevantMemories.OrderByDescending(m => m.Memory.Relevance))
        {
            var tokenCount = TokenUtils.TokenCount(result.Memory.Text);
            if (remainingTokens - tokenCount > 0)
            {
                if (!result.Memory.Tags.TryGetValue(MemoryTags.TagMemory, out var tags) || tags.Count <= 0) continue;
                
                var memoryName = tags.Single()!;
                var citationSource = CitationSource.FromSemanticMemoryCitation(result.Citation, result.Memory.Text, result.Memory.Relevance);

                if (_memoryNames.Contains(memoryName))
                {
                    if (!memoryMap.TryGetValue(memoryName, out var memories))
                    {
                        memories = new List<(string, CitationSource)>();
                        memoryMap.Add(memoryName, memories);
                    }
                        
                    memories.Add((result.Memory.Text, citationSource));
                    remainingTokens -= tokenCount;
                }
                    
                // Only documents will have citations.
                if (memoryName == _promptOptions.DocumentMemoryName)
                    citationMap.TryAdd(result.Citation.Link, citationSource);
            }
            else
                break;
        }

        return (memoryMap, citationMap);
    }

    /// <summary>
    /// Format long term and working memories.
    /// </summary>
    private void FormatMemories(IDictionary<string,List<(string, CitationSource)>> memoryMap, StringBuilder builderMemory)
    {
        foreach (var memoryName in _promptOptions.MemoryMap.Keys)
        {
            if (!memoryMap.TryGetValue(memoryName, out var memories)) continue;
            foreach (var (memoryContent, _) in memories)
            {
                if (builderMemory.Length == 0)
                    builderMemory.Append("Past memories (format: [memory type] <label>: <details>):\n");

                builderMemory.Append($"[{memoryName}] {memoryContent}\n");
            }
        }
    }

    /// <summary>
    /// Format document snippets.
    /// </summary>
    private void FormatSnippets(IDictionary<string,List<(string, CitationSource)>> memoryMap, StringBuilder builderMemory)
    {
        if (!memoryMap.TryGetValue(_promptOptions.DocumentMemoryName, out var memories) || memories.Count == 0) return;

        builderMemory.AppendLine("User has also shared some document snippets.");
        builderMemory.AppendLine("Quote the document link in square brackets at the end of each sentence that refers to the snippet in your response.");

        foreach (var (memoryContent, citation) in memories)
        {
            var memoryText = $"Document name: {citation.SourceName}\nDocument link: {citation.Link}\n[CONTENT START]\n{memoryContent}\n[CONTENT END]";
            builderMemory.AppendLine(memoryText);
        }
    }

    /// <summary>
    /// Calculates the relevance threshold for a given memory.
    /// The relevance threshold is a function of the memory balance.
    /// The memory balance is a value between 0 and 1, where 0 means maximum focus on working term memory (by minimizing the relevance threshold for working memory
    /// and maximizing the relevance threshold for long-term memory) and 1 means maximum focus on long-term memory (by maximizing the relevance threshold for working memory).
    /// The memory balance controls two 1st degree polynomials defined by the lower and upper bounds, on for long term memory and one for working memory.
    /// The relevance threshold is the value of the polynomial at the memory balance.
    /// </summary>
    /// <param name="memoryName">The name of the memory.</param>
    /// <param name="memoryBalance">The balance between long term memory and working term memory.</param>
    /// <exception cref="ArgumentException">Throws when the memory name is invalid.</exception>
    private float CalculateRelevanceThreshold(string memoryName, float memoryBalance)
    {
        var upper = _promptOptions.SemanticMemoryRelevanceUpper;
        var lower = _promptOptions.SemanticMemoryRelevanceLower;
        
        if (memoryBalance < 0.0 || memoryBalance > 1.0)
            throw new ArgumentException($"Memory balance must be between 0.0 and 1.0. Received {memoryBalance}.");

        if (memoryName == _promptOptions.LongTermMemoryName)
            return (lower - upper) * memoryBalance + upper;

        if (memoryName == _promptOptions.WorkingMemoryName)
            return (upper - lower) * memoryBalance + lower;
        
        if (memoryName == _promptOptions.DocumentMemoryName)
            return _promptOptions.DocumentMemoryMinRelevance;
        
        throw new ArgumentException($"Invalid memory name: {memoryName}");
    }
}
