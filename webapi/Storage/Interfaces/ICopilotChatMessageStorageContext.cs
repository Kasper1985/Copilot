using WebApi.Models.Storage;

namespace WebApi.Storage.Interfaces;

/// <summary>
/// Specialization of <see cref="IStorageContext{T}"/> for <see cref="CopilotChatMessage"/>.
/// </summary>
public interface ICopilotChatMessageStorageContext : IStorageContext<CopilotChatMessage>
{
    /// <summary>
    /// Query entities in the storage context.
    /// </summary>
    /// <param name="predicate">Predicate that needs to evaluate to true for a particular entry to be returned.</param>
    /// <param name="skip">Number of messages to skip before starting to return messages.</param>
    /// <param name="count">the number of messages to return. -1 returns all messages.</param>
    /// <returns>A list of <see cref="CopilotChatMessage"/>s matching the given predicate.</returns>
    Task<IEnumerable<CopilotChatMessage>> QueryEntities(Func<CopilotChatMessage, bool> predicate, int skip = 0, int count = -1);
}
