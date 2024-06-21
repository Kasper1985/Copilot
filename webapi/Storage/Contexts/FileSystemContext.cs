using System.Collections.Concurrent;
using System.Text.Json;
using WebApi.Storage.Interfaces;

namespace WebApi.Storage.Contexts;

/// <summary>
/// A storage context that stores entities on disk.
/// </summary>
public class FileSystemContext<T> : IStorageContext<T> where T : IStorageEntity
{
    protected sealed class EntityDictionary : ConcurrentDictionary<string, T>;
    
    /// <summary>
    /// The file path to store and read entities on disk.
    /// </summary>
    private readonly FileInfo _fileStorage;
    /// <summary>
    /// Using a concurrent dictionary to store entities in memory.
    /// </summary>
    protected readonly EntityDictionary Entities;
    /// <summary>
    /// A lock object to prevent concurrent access to the file storage.
    /// </summary>
    private readonly object _fileStorageLock = new();
    
    
    /// <summary>
    /// Initializes a new instance of the OnDiskContext class and load the entities from disk.
    /// </summary>
    /// <param name="filePath">The file path to store and read entities on disk.</param>
    public FileSystemContext(FileInfo filePath)
    {
        _fileStorage = filePath;
        Entities = Load(_fileStorage);
    }
    
    /// <inheritdoc/>
    public Task<IEnumerable<T>> QueryEntities(Func<T, bool> predicate) => Task.FromResult(Entities.Values.Where(predicate));
    
    /// <inheritdoc/>
    public Task Create(T entity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entity.Id);
        
        if (Entities.TryAdd(entity.Id, entity))
            Save(Entities, _fileStorage);
        
        return Task.CompletedTask;
    }
    
    /// <inheritdoc/>
    public Task Delete(T entity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entity.Id);
        
        if (Entities.TryRemove(entity.Id, out _))
            Save(Entities, _fileStorage);
        
        return Task.CompletedTask;
    }
    
    /// <inheritdoc/>
    public Task<T> Read(string entityId, string partitionKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entityId);
        
        return Entities.TryGetValue(entityId, out var entity) ? Task.FromResult(entity) : Task.FromException<T>(new KeyNotFoundException($"Entity with id {entityId} not found."));
    }

    /// <inheritdoc/>
    public Task Upsert(T entity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entity.Id);

        Entities.AddOrUpdate(entity.Id, entity, (_, _) => entity);
        Save(Entities, _fileStorage);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Save the state og the entities to disk.
    /// </summary>
    private void Save(EntityDictionary entities, FileInfo fileInfo)
    {
        lock (_fileStorageLock)
        {
            if (!fileInfo.Exists)
            {
                fileInfo.Directory!.Create();
                File.WriteAllText(fileInfo.FullName, "{}");
            }
            
            using var fileStream = File.Open(fileInfo.FullName, FileMode.OpenOrCreate, FileAccess.Write, FileShare.Read);
            JsonSerializer.Serialize(fileStream, entities);
        }
    }
    
    /// <summary>
    /// Load the state of entities from disk.
    /// </summary>
    /// <param name="fileInfo"></param>
    /// <returns></returns>
    private static EntityDictionary Load(FileInfo fileInfo)
    {
        if (!fileInfo.Exists)
        {
            fileInfo.Directory!.Create();
            File.WriteAllText(fileInfo.FullName, "{}");
        }
        
        using var fileStream = File.Open(fileInfo.FullName, FileMode.OpenOrCreate, FileAccess.Read, FileShare.Read);
        return JsonSerializer.Deserialize<EntityDictionary>(fileStream) ?? new EntityDictionary();
    }
}
