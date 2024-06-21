using System.Collections.Concurrent;
using System.Diagnostics;
using WebApi.Storage.Interfaces;

namespace WebApi.Storage.Contexts;

/// <summary>
/// A storage context that stores entities in memory.
/// </summary>
[DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
public class VolatileContext<T> : IStorageContext<T> where T : IStorageEntity
{
    protected readonly ConcurrentDictionary<string, T> Entities;
    
    /// <summary>
    /// Initializes a new instance of the InMemoryContext class.
    /// </summary>
    public VolatileContext()
    {
        Entities = new ConcurrentDictionary<string, T>();
    }
    
    /// <inheritdoc/>
    public Task<IEnumerable<T>> QueryEntities(Func<T, bool> predicate) => Task.FromResult(Entities.Values.Where(predicate));

    /// <inheritdoc/>
    public Task Create(T entity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entity.Id);
        
        Entities.TryAdd(entity.Id, entity);
        return Task.CompletedTask;
    }
    
    /// <inheritdoc/>
    public Task Delete(T entity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entity.Id);
        
        Entities.TryRemove(entity.Id, out _);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<T> Read(string entityId, string partitionKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entityId);

        if (Entities.TryGetValue(entityId, out var entity))
            return Task.FromResult(entity);
        
        throw new KeyNotFoundException($"Entity with ID '{entityId}' not found.");
    }
    
    /// <inheritdoc/>
    public Task Upsert(T entity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entity.Id);
        
        Entities.AddOrUpdate(entity.Id, entity, (key, oldValue) => entity);
        return Task.CompletedTask;
    }
    
    private string GetDebuggerDisplay() => ToString() ?? string.Empty;
}
