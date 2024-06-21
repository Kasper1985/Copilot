using WebApi.Models.Storage;
using WebApi.Storage.Interfaces;

namespace WebApi.Storage.Contexts;

public class FileSystemCopilotChatMessageContext(FileInfo filePath) : FileSystemContext<CopilotChatMessage>(filePath), ICopilotChatMessageStorageContext
{
    public Task<IEnumerable<CopilotChatMessage>> QueryEntities(Func<CopilotChatMessage, bool> predicate, int skip, int count) =>
        Task.Run(() => Entities.Values
            .Where(predicate)
            .OrderByDescending(m => m.Timestamp)
            .Skip(skip)
            .Take(count));
}
