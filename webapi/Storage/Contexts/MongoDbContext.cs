using WebApi.Storage.Interfaces;

namespace WebApi.Storage.Contexts;

/// <summary>
/// A storage context that stores entities in a MongoDB collection.
/// </summary>
public class MongoDbContext<T> : IStorageContext<T>, IDisposable where T : IStorageEntity
{
    /// <summary>
    /// Initializes a new instance of the MongoDbContext class.
    /// </summary>
    /// <param name="connectionString">The MongoDB connection string.</param>
    /// <param name="database">The MongoDB database name.</param>
    /// <param name="collection">The MongoDB collection name.</param>
    public MongoDbContext(string connectionString, string database, string collection)
    {
        // TODO: Implement the MongoDB context initialization.
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<T>> QueryEntities(Func<T, bool> predicate) => throw new NotImplementedException();
    
    /// <inheritdoc/>
    public async Task Create(T entity) => throw new NotImplementedException();
    
    /// <inheritdoc/>
    public async Task<T> Read(string entityId, string partitionKey) => throw new NotImplementedException();
    
    /// <inheritdoc/>
    public async Task Upsert(T entity) => throw new NotImplementedException();
    
    /// <inheritdoc/>
    public async Task Delete(T entity) => throw new NotImplementedException();

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        // TODO: dispose of the MongoDB client.
    }
}
