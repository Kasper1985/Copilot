using WebApi.Models.Storage;
using WebApi.Storage.Interfaces;

namespace WebApi.Storage.Contexts;

/// <summary>
/// Specializarion of <see cref="VolatileContext{T}"/> for <see cref="CopilotChatMessage"/>
/// </summary>
public class VolatileCopilotChatMessageContext : VolatileContext<CopilotChatMessage>, ICopilotChatMessageStorageContext
{
    /// <inheritdoc/>
    public Task<IEnumerable<CopilotChatMessage>> QueryEntities(Func<CopilotChatMessage, bool> predicate, int skip, int count) =>
        Task.Run(() => Entities.Values
            .Where(predicate).OrderByDescending(m => m.Timestamp)
            .Skip(skip)
            .Take(count));
}
