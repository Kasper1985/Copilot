namespace WebApi.Enums;

public enum ChatStoreType
{
    /// <summary>
    /// Non-persistent chat store
    /// </summary>
    Volatile,
    /// <summary>
    /// File-system based persistent chat store
    /// </summary>
    Filesystem,
    /// <summary>
    /// MongoDB based persistent chat store
    /// </summary>
    Mongo
}
