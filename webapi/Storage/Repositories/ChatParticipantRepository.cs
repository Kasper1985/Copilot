using WebApi.Models.Storage;
using WebApi.Storage.Interfaces;

namespace WebApi.Storage.Repositories;

/// <summary>
/// A repository for chat participants.
/// </summary>
/// <param name="storageContext">The storage context.</param>
public class ChatParticipantRepository(IStorageContext<ChatParticipant> storageContext) : Repository<ChatParticipant>(storageContext)
{
    /// <summary>
    /// Finds chat participant by user id.
    /// A user can be part of multiple chats, thus a user can have multiple chat participants.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <returns>A list of chat participants of the same user ID in different chat sessions.</returns>
    public Task<IEnumerable<ChatParticipant>> FindByUserId(string userId) => storageContext.QueryEntities(e => e.UserId == userId);
    
    /// <summary>
    /// Find chat participants by chat ID.
    /// </summary>
    /// <param name="chatId">The chat ID.</param>
    /// <returns>A list of chat participants in the same chat session.</returns>
    public Task<IEnumerable<ChatParticipant>> FindByChatId(string chatId) => storageContext.QueryEntities(e => e.ChatId == chatId);

    /// <summary>
    /// Checks if a user is in a chat session.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="chatId">The chat ID.</param>
    /// <returns>True if the user is in the chat session, otherwise - false.</returns>
    public async Task<bool> IsUserInChat(string userId, string chatId) => 
        await storageContext
            .QueryEntities(p => p.UserId == userId && p.ChatId == chatId)
            .ContinueWith(t => t.Result.Any());
}
