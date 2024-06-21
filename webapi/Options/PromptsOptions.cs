using System.ComponentModel.DataAnnotations;
using WebApi.Attributes;
using WebApi.Enums;

namespace WebApi.Options;

/// <summary>
/// Configuration options for the chat.
/// </summary>
public class PromptsOptions
{
    public const string PropertyName = "Prompts";
    
    /// <summary>
    /// Token limit of the chat model.
    /// </summary>
    [Required, Range(0, int.MaxValue)] public int CompletionTokenLimit { get; set; }
    
    /// <summary>
    /// The token count left for the model to generate text after the prompt.
    /// </summary>
    [Required, Range(0, int.MaxValue)] public int ResponseTokenLimit { get; set; }

    /// <summary>
    /// The token count allowed for function calling responses.
    /// </summary>
    [Required, Range(0, int.MaxValue)] public int FunctionCallingTokenLimit { get; set; }
    
    /// <summary>
    /// Weight of memories in the contextual part of the final prompt.
    /// Contextual prompt excludes all the system commands and user intent.
    /// </summary>
    internal double MemoriesResponseContextWeight { get; } = 0.6;
    
    /// <summary>
    /// Upper bound of relevance score of a kernel memory to be included in the final prompt.
    /// The actual relevancy score is determined by the memory balance.
    /// </summary>
    internal float SemanticMemoryRelevanceUpper { get; } = 0.9F;
    
    /// <summary>
    /// Lower bound of relevance score of a kernel memory to be included in the final prompt.
    /// The actual relevancy score is determined by the memory balance.
    /// </summary>
    internal float SemanticMemoryRelevanceLower { get; } = 0.6F;
    
    /// <summary>
    /// Minimum relevance of a document memory to be included in the final prompt.
    /// The higher the value, the answer will be more relevant to the user intent.
    /// </summary>
    internal float DocumentMemoryMinRelevance { get; } = 0.66F;
    
    // System
    [Required, NotEmptyOrWhitespace] public string KnowledgeCutoffDate { get; set; } = string.Empty;
    [Required, NotEmptyOrWhitespace] public string InitialBotMessage { get; set; } = string.Empty;
    [Required, NotEmptyOrWhitespace] public string SystemDescription { get; set; } = string.Empty;
    [Required, NotEmptyOrWhitespace] public string SystemResponse { get; set; } = string.Empty;
    
    // Intent extraction
    [Required, NotEmptyOrWhitespace] public string SystemIntent { get; set; } = string.Empty;
    [Required, NotEmptyOrWhitespace] public string SystemIntentContinuation { get; set; } = string.Empty;
    
    // Audience extraction
    [Required, NotEmptyOrWhitespace] public string SystemAudience { get; set; } = string.Empty;
    [Required, NotEmptyOrWhitespace] public string SystemAudienceContinuation { get; set; } = string.Empty;
    
    // Memory storage
    [Required, NotEmptyOrWhitespace] public string MemoryIndexName { get; set; } = string.Empty;
    
    // Document memory
    [Required, NotEmptyOrWhitespace] public string DocumentMemoryName { get; set; } = string.Empty;
    
    // Memory extraction
    [Required, NotEmptyOrWhitespace] public string SystemCognitive { get; set; } = string.Empty;
    [Required, NotEmptyOrWhitespace] public string MemoryFormat { get; set; } = string.Empty;
    [Required, NotEmptyOrWhitespace] public string MemoryAntiHallucination { get; set; } = string.Empty;
    [Required, NotEmptyOrWhitespace] public string MemoryContinuation { get; set; } = string.Empty;
    
    // Long-term memory
    [Required, NotEmptyOrWhitespace] public string LongTermMemoryName { get; set; } = string.Empty;
    [Required, NotEmptyOrWhitespace] public string LongTermMemoryExtraction { get; set; } = string.Empty;
    
    // Working memory
    [Required, NotEmptyOrWhitespace] public string WorkingMemoryName { get; set; } = string.Empty;
    [Required, NotEmptyOrWhitespace] public string WorkingMemoryExtraction { get; set; } = string.Empty;
    
    // Response
    internal double ResponseTemperature { get; } = 0.7;
    internal double ResponseTopP { get; } = 1;
    internal double ResponsePresencePenalty { get; } = 0.5;
    internal double ResponseFrequencyPenalty { get; } = 0.5;

    // Intent
    internal double IntentTemperature { get; } = 0.7;
    internal double IntentTopP { get; } = 1;
    internal double IntentPresencePenalty { get; } = 0.5;
    internal double IntentFrequencyPenalty { get; } = 0.5;

    private string[] SystemAudiencePromptComponents =>
    [
        SystemAudience,
        "{{ChatPlugin.ExtractChatHistory}}",
        SystemAudienceContinuation
    ];
    internal string SystemAudienceExtraction => string.Join("\n", SystemAudiencePromptComponents);
    
    private string[] SystemIntentPromptComponents =>
    [
        SystemDescription,
        SystemIntent,
        "{{ChatPlugin.ExtractChatHistory}}",
        SystemIntentContinuation
    ];
    internal string SystemIntentExtraction => string.Join("\n", SystemIntentPromptComponents);
    
    private string[] LongTermMemoryPromptComponents =>
    [
        SystemCognitive,
        $"{LongTermMemoryName} Description:\n{LongTermMemoryExtraction}",
        MemoryAntiHallucination,
        $"Chat Description:\n{SystemDescription}",
        "{{ChatPlugin.ExtractChatHistory}}",
        MemoryContinuation
    ];
    private string LongTermMemory => string.Join("\n", LongTermMemoryPromptComponents);
    
    private string[] WorkingMemoryPromptComponents =>
    [
        SystemCognitive,
        $"{WorkingMemoryName} Description:\n{WorkingMemoryExtraction}",
        MemoryAntiHallucination,
        $"Chat Description:\n{SystemDescription}",
        "{{ChatPlugin.ExtractChatHistory}}",
        MemoryContinuation
    ];
    private string WorkingMemory => string.Join("\n", WorkingMemoryPromptComponents);
    
    
    internal IDictionary<string, string> MemoryMap => new Dictionary<string, string>
    {
        { LongTermMemoryName, LongTermMemory },
        { WorkingMemoryName, WorkingMemory }
    };
    
    private string[] SystemPersonaComponents =>
    [
        SystemDescription,
        SystemResponse
    ];
    internal string SystemPersona => string.Join("\n\n", SystemPersonaComponents);
    
    /// <summary>
    /// Copy the options in case they need to be modified per chat.
    /// </summary>
    /// <returns>A shallow copy og the options.</returns>
    internal PromptsOptions Copy() => (PromptsOptions)MemberwiseClone();
    
    /// <summary>
    /// Tries to retrieve the memory container associated with the specified memory type.
    /// </summary>
    internal bool TryGetMemoryContainerName(string memoryType, out string memoryContainerName)
    {
        memoryContainerName = string.Empty;
        if (!Enum.TryParse(memoryType, ignoreCase: true, out SemanticMemoryType semanticMemoryType))
            return false;

        switch (semanticMemoryType)
        {
            case SemanticMemoryType.LongTermMemory:
                memoryContainerName = LongTermMemoryName;
                return true;
            
            case SemanticMemoryType.WorkingMemory:
                memoryContainerName = WorkingMemoryName;
                return true;
            
            default:
                return false;
        }
    }
}
