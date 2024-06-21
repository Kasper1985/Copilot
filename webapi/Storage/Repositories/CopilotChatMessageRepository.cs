using WebApi.Models.Storage;
using WebApi.Storage.Interfaces;

namespace WebApi.Storage.Repositories;

/// <summary>
/// Specialization of the <see cref="Repository{T}"/> class for <see cref="CopilotChatMessage"/>s.
/// </summary>
/// <param name="storageContext">The storage context.</param>
public class CopilotChatMessageRepository(ICopilotChatMessageStorageContext storageContext) : Repository<CopilotChatMessage>(storageContext)
{
    /// <summary>
    /// Finds chat messages matching a predicate.
    /// </summary>
    /// <param name="predicate">Predicate that needs to evaluate to true for a particular entry to be returned.</param>
    /// <param name="skip">Number of messages to skip before starting to return messages.</param>
    /// <param name="count">The number of messages to return. -1 returns all messages.</param>
    /// <returns>A list of <see cref="CopilotChatMessage"/>s matching the given predicate.</returns>
    public async Task<IEnumerable<CopilotChatMessage>> QueryEntities(Func<CopilotChatMessage, bool> predicate, int skip = 0, int count = -1) =>
        await Task.Run(() => storageContext.QueryEntities(predicate, skip, count));
    
    /// <summary>
    /// Finds chat messages by chat id.
    /// </summary>
    /// <param name="chatId">The chat ID.</param>
    /// <param name="skip">Number of messages to skip before starting to return messages.</param>
    /// <param name="count">The number of messages to return. -1 returns all messages.</param>
    /// <returns>A list of <see cref="CopilotChatMessage"/>s matching the given chat ID.</returns>
    public Task<IEnumerable<CopilotChatMessage>> FindByChatId(string chatId, int skip = 0, int count = -1) 
        => QueryEntities(m => m.ChatId == chatId, skip, count);

    /// <summary>
    /// Finds the most recent chat message by chat ID.
    /// </summary>
    /// <param name="chatId">The chat ID.</param>
    /// <returns>The most recent ChatMessage matching the given chat ID.</returns>
    /// <exception cref="KeyNotFoundException">Will be thrown if there is no messaged for given chat ID.</exception>
    public async Task<CopilotChatMessage> FindLastByChatId(string chatId)
    {
        var chatMessages = await FindByChatId(chatId, 0, 1);
        return chatMessages.FirstOrDefault() ?? throw new KeyNotFoundException($"No messages found for chat '{chatId}'.");
    }
}
