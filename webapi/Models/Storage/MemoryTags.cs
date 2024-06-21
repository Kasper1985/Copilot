namespace WebApi.Models.Storage;

/// <summary>
/// Tag names for kernel memory.
/// </summary>
public static class MemoryTags
{
    /// <summary>
    /// Associates memory with a specific chat.
    /// </summary>
    public const string TagChatId = "chatid";
    
    /// <summary>
    /// Associates memory with specific type.
    /// </summary>
    public const string TagMemory = "memory";
}
