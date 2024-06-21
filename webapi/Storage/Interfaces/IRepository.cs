namespace WebApi.Storage.Interfaces;

/// <summary>
/// Defines the basic CRUD operations for a repository.
/// </summary>
public interface IRepository<T> where T : IStorageEntity
{
    /// <summary>
    /// Finds an entity by its id.
    /// </summary>
    /// <param name="id">ID of the entity.</param>
    /// <param name="partition">Partition key value of the entity.</param>
    /// <returns>An entity.</returns>
    Task<T> FindById(string id, string partition);
    
    /// <summary>
    /// Tried to find an entity by its id.
    /// </summary>
    /// <param name="id">ID of the entity.</param>
    /// <param name="partition">Partition key value of the entity.</param>
    /// <param name="callback">The entity delegate. Note async methods don't support ref or out parameters.</param>
    /// <returns>True if the entity was found, otherwise - false.</returns>
    Task<bool> TryFindById(string id, string partition, Action<T?> callback);
    
    /// <summary>
    /// Creates a new entity in the repository.
    /// </summary>
    /// <param name="entity">An entity of type T.</param>
    Task Create(T entity);
    
    /// <summary>
    /// Updates/inserts an entity in the repository.
    /// </summary>
    /// <param name="entity">The entity to be updated/inserted.</param>
    Task Upsert(T entity);
    
    /// <summary>
    /// Deletes an entity from the repository.
    /// </summary>
    /// <param name="entity">-The entity to delete.</param>
    Task Delete(T entity);
}
