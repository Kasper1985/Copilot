using WebApi.Models.Storage;
using WebApi.Storage.Interfaces;

namespace WebApi.Storage.Repositories;

/// <summary>
/// A repository for memory sources.
/// </summary>
/// <param name="storageContext">The storage context.</param>
public class ChatMemorySourceRepository(IStorageContext<MemorySource> storageContext) : Repository<MemorySource>(storageContext)
{
    /// <summary>
    /// Finds chat memory sources by chat session ID.
    /// </summary>
    /// <param name="chatId">The chat session ID.</param>
    /// <param name="includeGlobal">Flag specifying if global documents should be included int the response.</param>
    /// <returns>A list of memory sources.</returns>
    public Task<IEnumerable<MemorySource>> FindByChatId(string chatId, bool includeGlobal = true) =>
        storageContext.QueryEntities(s => s.ChatId == chatId || (includeGlobal && s.ChatId == Guid.Empty.ToString()));
    
    /// <summary>
    /// Finds chat memory sources by name.
    /// </summary>
    /// <param name="name">Name of the memory source.</param>
    /// <returns>A list of memory sources with the give name.</returns>
    public Task<IEnumerable<MemorySource>> FindByName(string name) =>
        storageContext.QueryEntities(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    
    /// <summary>
    /// Retrieves all memory sources.
    /// </summary>
    /// <returns>A list of memory sources with the given name.</returns>
    public Task<IEnumerable<MemorySource>> GetAll() => storageContext.QueryEntities(_ => true);
}
