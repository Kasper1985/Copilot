using WebApi.Attributes;
using WebApi.Enums;

namespace WebApi.Options;

/// <summary>
/// Configuration settings for the chat store.
/// </summary>
public class ChatStoreOptions
{
    public const string SectionName = "AzureSpeech";
    
    /// <summary>
    /// Gets or inits the type of chat store to use
    /// </summary>
    public ChatStoreType Type { get; set; } = ChatStoreType.Volatile;
    /// <summary>
    /// Gets or inits the configuration for the file system chat store
    /// </summary>
    [RequiredOnPropertyValue(nameof(Type), ChatStoreType.Filesystem)]
    public FileSystemOptions? FileSystem { get; set; }
    [RequiredOnPropertyValue(nameof(Type), ChatStoreType.Mongo)]
    public MongoOptions? Mongo { get; set; }
}
