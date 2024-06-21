using WebApi.Storage.Interfaces;

namespace WebApi.Storage.Repositories;

/// <summary>
/// Defines the basic CRUD operations for a repository.
/// </summary>
/// <param name="storageContext">The storage context.</param>
public class Repository<T>(IStorageContext<T> storageContext) : IRepository<T> where T : IStorageEntity
{
    /// <inheritdoc/>
    public Task<T> FindById(string id, string? partition = null) => storageContext.Read(id, partition ?? id);
    
    /// <inheritdoc/>
    public async Task<bool> TryFindById(string id, string? partition = null, Action<T?>? callback = null)
    {
        try
        {
            var found = await FindById(id, partition);
            callback?.Invoke(found);

            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or KeyNotFoundException)
        {
            return false;
        }
    }
    
    /// <inheritdoc/>
    public Task Create(T entity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entity.Id);
        return storageContext.Create(entity);
    }

    /// <inheritdoc/>
    public Task Delete(T entity) => storageContext.Delete(entity);

    /// <inheritdoc/>
    public Task Upsert(T entity) => storageContext.Upsert(entity);
}
