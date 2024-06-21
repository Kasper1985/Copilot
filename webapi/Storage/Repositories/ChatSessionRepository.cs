using WebApi.Models.Storage;
using WebApi.Storage.Interfaces;

namespace WebApi.Storage.Repositories;

/// <summary>
/// A repository for chat sessions.
/// </summary>
/// <param name="storageContext">The storage context.</param>
public class ChatSessionRepository(IStorageContext<ChatSession> storageContext) : Repository<ChatSession>(storageContext)
{
    /// <summary>
    /// Retrieves all chat sessions.
    /// </summary>
    /// <returns>A list of ChatMessages.</returns>
    public Task<IEnumerable<ChatSession>> GetAllChats() => storageContext.QueryEntities(_ => true);
}
