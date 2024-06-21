using System.Text.Json.Serialization;
using WebApi.Storage.Interfaces;

namespace WebApi.Models.Storage;

public class ChatSession(string title, string systemDescription) : IStorageEntity
{
    private const string CurrentVersion = "2.0";
    
    /// <summary>
    /// Chat ID that is persistent and unique.
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Title of the chat.
    /// </summary>
    public string Title { get; set; } = title;
    
    /// <summary>
    /// Timestamp of the chat creation.
    /// </summary>
    public DateTimeOffset CreatedOn { get; set; }

    /// <summary>
    /// System description of the chat that is used to generate responses.
    /// </summary>
    public string SystemDescription { get; set; } = systemDescription;
    
    /// <summary>
    /// Fixed system description with "TimeSkill" replaced by "TimePlugin"
    /// </summary>
    public string SafeSystemDescription => SystemDescription.Replace("TimeSkill", "TimePlugin", StringComparison.OrdinalIgnoreCase);
    
    /// <summary>
    /// The balance between long term memory and working term memory.
    /// The higher this value, the more the system will rely on long term memory by lowering
    /// the relevance threshold of long term memory and increasing the threshold score of working memory.
    /// </summary>
    public float MemoryBalance { get; set; } = 0.5F;
    
    /// <summary>
    /// A list of enabled plugins
    /// </summary>
    public HashSet<string> EnabledPlugins { get; set; } = [];

    /// <summary>
    /// Used to determine if the current chat requires upgrade.
    /// </summary>
    public string? Version { get; set; } = CurrentVersion;
    
    /// <summary>
    /// The partition key for the session.
    /// </summary>
    [JsonIgnore]
    public string Partition => Id;
}
