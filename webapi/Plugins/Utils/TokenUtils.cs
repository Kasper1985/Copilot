using System.Globalization;
using System.Text.Json;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace WebApi.Plugins.Utils;

/// <summary>
/// Utility methods for token management.
/// </summary>
public class TokenUtils
{
    private static readonly SharpToken.GptEncoding Tokenizer = SharpToken.GptEncoding.GetEncoding("cl100k_base");

    /// <summary>
    /// Semantic dependencies of ChatPlugin.
    /// If you add a new semantic dependency, please add it here.
    /// </summary>
    internal static readonly Dictionary<string , string> SemanticFunctions = new()
    {
        { "SystemAudienceExtraction", "audienceExtraction" },
        { "SystemIntentExtraction", "userIntentExtraction" },
        { "SystemMetaPrompt", "metaPromptTemplate" },
        { "SystemCompletion", "responseCompletion" },
        { "SystemCognitive_WorkingMemory", "workingMemoryExtraction" },
        { "SystemCognitive_LongTermMemory", "longTermMemoryExtraction" }
    };
    
    /// <summary>
    /// Gets dictionary containing empty token usage totals.
    /// Use for responses that are hardcoded and/or do not have semantic (token) dependencies.
    /// </summary>
    /// <returns></returns>
    internal static Dictionary<string, int> EmptyTokenUsages() => SemanticFunctions.Values.ToDictionary(v => v, v => 0);
    
    /// <summary>
    /// Gets key used to identify function token usage in context variables.
    /// </summary>
    /// <param name="functionName">Name of semantic function.</param>
    /// <returns>The key corresponding to the semantic name, or null if the function name is unknown.</returns>
    internal static string GetFunctionKey(string? functionName)
    {
        if (functionName is null || !SemanticFunctions.TryGetValue(functionName, out var key))
            throw new KeyNotFoundException($"Unknown token dependency {functionName}. Please define function as semanticFunctions entry in TokenUtils.cs");

        return $"{key}TokenUsage";
    }

    /// <summary>
    /// Gets the total token usage from a Chat ot Text Completion result context and adds it as a variable to response context.
    /// </summary>
    /// <param name="result">Result context from chat model.</param>
    /// <param name="logger">The logger instance to use for logging errors.</param>
    /// <returns>String representation of number of tokens used by function (or null on error).</returns>
    internal static string? GetFunctionTokenUsage(FunctionResult result, ILogger logger)
    {
        if (result.Metadata is null || !result.Metadata.TryGetValue("Usage", out var usageObject) || usageObject is null)
        {
            logger.LogError("No usage metadata provided in function result.");
            return null;
        }

        var tokenUsage = 0;
        try
        {
            var jsonObject = JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(usageObject));
            tokenUsage = jsonObject.GetProperty("TotalTokens").GetInt32();
        }
        catch (KeyNotFoundException)
        {
            logger.LogError("Usage details not found in model result.");
            return null;
        }

        return tokenUsage.ToString(CultureInfo.InvariantCulture);
    }
    
    /// <summary>
    /// Rough token costing of <see cref="ChatHistory"/> object.
    /// </summary>
    /// <param name="chatHistory"><see cref="ChatHistory"/> object to calculate the number of tokens of.</param>
    internal static int GetContextMessagesTokenCount(ChatHistory chatHistory) => chatHistory.Sum(m => GetContextMessageTokenCount(m.Role, m.Content));

    /// <summary>
    /// Calculates the number of tokens in a string using custom SharpToken token counter implementation with cl100k_base encoding.
    /// </summary>
    /// <param name="text">The string to calculate the number of tokens in.</param>
    internal static int TokenCount(string text)
    {
        var tokens = Tokenizer.Encode(text);
        return tokens.Count;
    }

    /// <summary>
    /// Rough token costing of message object.
    /// Follows the syntax defined by Azure OpenAI's ChatMessage object, e.g. "message": { "role": "assistant", "content": "Yes" }
    /// </summary>
    /// <param name="authorRole"><see cref="AuthorRole"/> of the message.</param>
    /// <param name="content">Content of the message.</param>
    internal static int GetContextMessageTokenCount(AuthorRole authorRole, string? content) => TokenCount($"role:{authorRole.Label}") + TokenCount($"content:{content}\n");
}
