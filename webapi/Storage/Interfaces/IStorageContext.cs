namespace WebApi.Storage.Interfaces;

/// <summary>
/// Defines the basic CRUD operations for a storage context.
/// </summary>
public interface IStorageContext<T> where T : IStorageEntity
{
    /// <summary>
    /// Query entities in the storage context.
    /// </summary>
    /// <param name="predicate">Predicate that needs to evaluate to true for a particular entry to be returned.</param>
    /// <returns><see cref="IEnumerable{T}"/> of entities.</returns>
    Task<IEnumerable<T>> QueryEntities(Func<T, bool> predicate);
    
    /// <summary>
    /// Read an entity from the storage context by id.
    /// </summary>
    /// <param name="entityId">The entity id.</param>
    /// <param name="partitionKey">The entity partition.</param>
    /// <returns>The entity.</returns>
    Task<T> Read(string entityId, string partitionKey);
    
    /// <summary>
    /// Create an entity in the storage context.
    /// </summary>
    /// <param name="entity">The entity to be created in the context.</param>
    Task Create(T entity);
    
    /// <summary>
    /// Update/insert an entity in the storage context.
    /// </summary>
    /// <param name="entity">The entity to be updated/inserted in the context.</param>
    Task Upsert(T entity);
    
    /// <summary>
    /// Delete an entity from the storage context.
    /// </summary>
    /// <param name="entity">the entity to be deleted from the context.</param>
    Task Delete(T entity);
}
