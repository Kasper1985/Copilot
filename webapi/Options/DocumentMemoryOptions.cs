using System.ComponentModel.DataAnnotations;

namespace WebApi.Options;

public class DocumentMemoryOptions
{
    public const string PropertyName = "DocumentMemory";
    
    /// <summary>
    /// Global documents will be tagged by an empty Guid as chat-id ("00000000-0000-0000-0000-000000000000").
    /// </summary>
    internal static readonly Guid GlobalDocumentChatId = Guid.Empty;
    
    /// <summary>
    /// Gets or sets the maximum number of tokens to use when splitting a document into "lines".
    /// </summary>
    [Range(0, int.MaxValue)] public int DocumentLineSplitMaxTokens { get; set; } = 30;
    
    /// <summary>
    /// Gets or sets the maximum number of tokens to use when splitting documents for embeddings.
    /// </summary>
    [Range(0, int.MaxValue)] public int DocumentChunkMaxTokens { get; set; } = 100;
    
    /// <summary>
    /// Maximum size in bytes of a document to be allowed for importing.
    /// </summary>
    [Range(0, int.MaxValue)] public int FileSizeLimit { get; set; } = 1000000;
    
    /// <summary>
    /// Maximum number of files to be allowed for importing in a single request.
    /// </summary>
    [Range(0, int.MaxValue)] public int FileCountLimit { get; set; } = 10;
}
