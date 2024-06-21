using WebApi.Models.Storage;
using WebApi.Storage.Interfaces;

namespace WebApi.Storage.Contexts;

/// <summary>
/// Specialization of the <see cref="MongoDbContext{T}"/> class for CopilotChatMessage entities.
/// </summary>
/// <param name="connectionString">The MongoDB connection string.</param>
/// <param name="database">The MongoDB database name.</param>
/// <param name="collection">The MongoDB collection name.</param>
public class MongoDbCopilotChatMessageContext(string connectionString, string database, string collection)
    : MongoDbContext<CopilotChatMessage>(connectionString, database, collection), ICopilotChatMessageStorageContext
{
    public Task<IEnumerable<CopilotChatMessage>> QueryEntities(Func<CopilotChatMessage, bool> predicate, int skip, int count) => throw new NotImplementedException();
}
