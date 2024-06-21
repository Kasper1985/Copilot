using Microsoft.Extensions.Options;
using WebApi.Enums;
using WebApi.Models.Storage;
using WebApi.Options;
using WebApi.Services.Interfaces;
using WebApi.Storage.Contexts;
using WebApi.Storage.Interfaces;
using WebApi.Storage.Repositories;

namespace WebApi.Extensions;

internal static class ServiceExtensions
{
    internal static IServiceCollection AddOptions(this IServiceCollection services, IConfiguration configuration)
    {
        // Azure Speech token configuration
        AddOptionsForSection<AzureSpeechOptions>(AzureSpeechOptions.SectionName);
        // Chat storage configuration
        AddOptionsForSection<ChatStoreOptions>(ChatStoreOptions.SectionName);
        
        return services;
        
        // Local function for simplifying the addition of options for a given section
        void AddOptionsForSection<TOptions>(string sectionName) where TOptions : class
        {
            services.AddOptions<TOptions>(configuration.GetSection(sectionName));
        }
    }
    
    /// <summary>
    /// Add persistent chat store services.
    /// </summary>
    internal static IServiceCollection AddPersistentChatStore(this IServiceCollection services)
    {
        IStorageContext<ChatSession> chatSessionStorageContext;
        ICopilotChatMessageStorageContext chatMessageStorageContext;
        IStorageContext<MemorySource> chatMemorySourceStorageContext;
        IStorageContext<ChatParticipant> chatParticipantStorageContext;
        
        var chatStoreOptions = services.BuildServiceProvider().GetRequiredService<IOptions<ChatStoreOptions>>().Value;
        switch (chatStoreOptions.Type)
        {
            case ChatStoreType.Volatile:
                chatSessionStorageContext = new VolatileContext<ChatSession>();
                chatMessageStorageContext = new VolatileCopilotChatMessageContext();
                chatMemorySourceStorageContext = new VolatileContext<MemorySource>();
                chatParticipantStorageContext = new VolatileContext<ChatParticipant>();
                break;
            case ChatStoreType.Filesystem:
                if (chatStoreOptions.FileSystem is null)
                    throw new InvalidOperationException("ChatStore: Filesystem is required when ChatStoreType is set to 'Filesystem'");
                
                var fullPath = Path.GetFullPath(chatStoreOptions.FileSystem.FilePath);
                var directory = Path.GetDirectoryName(fullPath) ?? string.Empty;
                var filePath = Path.Combine(directory, $"{Path.GetFileNameWithoutExtension(fullPath)}_placeholder_{Path.GetExtension(fullPath)}");
                chatSessionStorageContext = new FileSystemContext<ChatSession>(new FileInfo(filePath.Replace("_placeholder_", "_sessions")));
                chatMessageStorageContext = new FileSystemCopilotChatMessageContext(new FileInfo(filePath.Replace("_placeholder_", "_messages")));
                chatMemorySourceStorageContext = new FileSystemContext<MemorySource>(new FileInfo(filePath.Replace("_placeholder_", "_memorysources")));
                chatParticipantStorageContext = new FileSystemContext<ChatParticipant>(new FileInfo(filePath.Replace("_placeholder_", "_participants")));
                break;
            case ChatStoreType.Mongo:
                if (chatStoreOptions.Mongo is null)
                    throw new InvalidOperationException("ChatStore: Mongo is required when ChatStoreType is set to 'Mongo'");
                
                chatSessionStorageContext = new MongoDbContext<ChatSession>(chatStoreOptions.Mongo.ConnectionString, chatStoreOptions.Mongo.Database, chatStoreOptions.Mongo.ChatSessionsCollection);
                chatMessageStorageContext = new MongoDbCopilotChatMessageContext(chatStoreOptions.Mongo.ConnectionString, chatStoreOptions.Mongo.Database, chatStoreOptions.Mongo.ChatMessagesCollection);
                chatMemorySourceStorageContext = new MongoDbContext<MemorySource>(chatStoreOptions.Mongo.ConnectionString, chatStoreOptions.Mongo.Database, chatStoreOptions.Mongo.ChatMemorySourcesCollection);
                chatParticipantStorageContext = new MongoDbContext<ChatParticipant>(chatStoreOptions.Mongo.ConnectionString, chatStoreOptions.Mongo.Database, chatStoreOptions.Mongo.ChatParticipantsCollection);
                break;
            default:
                throw new InvalidOperationException($"ChatStore: Unknown ChatStoreType '{chatStoreOptions.Type}'");
        }

        services.AddSingleton(new ChatSessionRepository(chatSessionStorageContext));
        services.AddSingleton(new CopilotChatMessageRepository(chatMessageStorageContext));
        services.AddSingleton(new ChatMemorySourceRepository(chatMemorySourceStorageContext));
        services.AddSingleton(new ChatParticipantRepository(chatParticipantStorageContext));

        return services;
    }
    
    internal static IServiceCollection AddMaintenanceServices(this IServiceCollection services)
    {
        // Inject action stub
        services.AddSingleton<IReadOnlyList<IMaintenanceAction>>(_ => Array.Empty<IMaintenanceAction>());
        return services;
    }
    
    /// <summary>
    /// Add CORS settings.
    /// </summary>
    internal static IServiceCollection AddCorsPolicy(this IServiceCollection services, IConfiguration configuration)
    {
        var allowedOrigins = configuration.GetSection("AllowedOrigins").Get<string[]>() ?? [];
        if (allowedOrigins.Length > 0)
        {
            services.AddCors(options =>
            {
                options.AddDefaultPolicy(
                    policy =>
                    {
                        policy.WithOrigins(allowedOrigins)
                            .WithMethods("POST", "GET", "PUT", "DELETE", "PATCH")
                            .AllowAnyHeader();
                    });
            });
        }

        return services;
    }
    
    private static void AddOptions<TOptions>(this IServiceCollection services, IConfigurationSection section) where TOptions : class
    {
        services.AddOptions<TOptions>()
            .Bind(section)
            .ValidateDataAnnotations()
            .ValidateOnStart()
            .PostConfigure(TrimStringProperties);
    }

    /// <summary>
    /// Trim all string properties of the given options object recursively.
    /// </summary>
    private static void TrimStringProperties<T>(T options) where T : class
    {
        var targets = new Queue<object>();
        targets.Enqueue(options);
        
        while (targets.Count > 0)
        {
            var target = targets.Dequeue();
            var targetType = target.GetType();
            foreach (var property in targetType.GetProperties())
            {
                // Skip enumerations
                if (property.PropertyType.IsEnum) continue;
                // Skip index properties
                if (property.GetIndexParameters().Length > 0) continue;
                
                // Property is a build-in type, readable and writable
                if (property.PropertyType.Namespace == "System" && property is { CanRead: true, CanWrite: true })
                {
                    // Property is a non-null string
                    if (property.PropertyType == typeof(string) && property.GetValue(target) is not null)
                        property.SetValue(target, property.GetValue(target)!.ToString()!.Trim());
                }
                else
                {
                    // Property is a non-built-in and non-enum type - queue it for further processing
                    if (property.GetValue(target) is not null)
                        targets.Enqueue(property.GetValue(target)!);
                }
            }
        }
    }
}
