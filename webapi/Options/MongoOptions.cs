using System.ComponentModel.DataAnnotations;
using WebApi.Attributes;

namespace WebApi.Options;

public class MongoOptions
{
    /// <summary>
    /// Gets or inits the Mongo database name
    /// </summary>
    [Required, NotEmptyOrWhitespace]
    public string Database { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or inits the Mongo connection string
    /// </summary>
    [Required, NotEmptyOrWhitespace]
    public string ConnectionString { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or inits the Mongo collection name for chat sessions
    /// </summary>
    public string ChatSessionsCollection { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or inits the Mongo collection name for chat messages
    /// </summary>
    public string ChatMessagesCollection { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or inits the Mongo collection name for chat memory sources
    /// </summary>
    public string ChatMemorySourcesCollection { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or inits the Mongo collection name for chat participants
    /// </summary>
    public string ChatParticipantsCollection { get; set; } = string.Empty;
}
