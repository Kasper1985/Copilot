using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.KernelMemory;
using Microsoft.KernelMemory.AI;
using Microsoft.KernelMemory.DocumentStorage.DevTools;
using Microsoft.KernelMemory.MemoryStorage;
using Microsoft.KernelMemory.MemoryStorage.DevTools;
using Microsoft.KernelMemory.Pipeline.Queue.DevTools;
using Shared.Extensions;

namespace Shared;

internal sealed class ServiceConfiguration
{
    // appsettings.json root node name
    private const string ConfigRoot = "KernelMemory";
    // ASP.NET env var
    private const string AspNetEnvVar = "ASPNETCORE_ENVIRONMENT";
    // OpenAI env var
    private const string OpenAIEnvVar = "OPENAI_API_KEY";
    // Content of appsettings.json, used to access dynamic data under "Services"
    private IConfiguration _rawAppSettings;
    // Normalized configuration
    private KernelMemoryConfig _memoryConfiguration;
    
    public ServiceConfiguration(IConfiguration rawAppSettings, KernelMemoryConfig memoryConfiguration)
    {
        _rawAppSettings = rawAppSettings ?? throw new ArgumentNullException(nameof(rawAppSettings), "Configuration is required.");
        _memoryConfiguration = memoryConfiguration ?? throw new ArgumentNullException(nameof(memoryConfiguration), "Memory configuration is required.");
        
        if (!MinimumConfigurationIsAvailable(false)) { SetupForOpenAI(); }

        MinimumConfigurationIsAvailable(true);
    }
    
    public ServiceConfiguration(string? settingsDirectory = null) : this(ReadAppSettings(settingsDirectory))
    { }

    private ServiceConfiguration(IConfiguration rawAppSettings) : this(rawAppSettings, rawAppSettings.GetSection(ConfigRoot).Get<KernelMemoryConfig>()
        ?? throw new ConfigurationException($"Unable to load Kernel Memory settings from the given configuration. There should be a '{ConfigRoot}' root node, with data mapping to '{nameof(KernelMemoryConfig)}'"))
    { }
    
    public IKernelMemoryBuilder PrepareBuilder(IKernelMemoryBuilder builder) => BuildUsingConfiguration(builder);

    /// <summary>
    /// Check the configuration for minimum requirements.
    /// </summary>
    /// <param name="throwOnError">Whether to throw or return false when the config is incomplete.</param>
    /// <returns>Whether the configuration is valid.</returns>
    private bool MinimumConfigurationIsAvailable(bool throwOnError)
    {
        // Check if text generation settings are available
        if (string.IsNullOrWhiteSpace(_memoryConfiguration.TextGeneratorType))
        {
            if (!throwOnError) { return false; }
            throw new InvalidOperationException("Text generation (TextGeneratorType) is not configured in Kernel Memory.");
        }
        
        // Check embedding generation ingestion settings
        if (_memoryConfiguration.DataIngestion is { EmbeddingGenerationEnabled: true, EmbeddingGeneratorTypes.Count: 0 })
        {
            if (!throwOnError) { return false; }
            throw new ConfigurationException("Data ingestion embedding generation (DataIngestion.EmbeddingGeneratorTypes) is not configured in Kernel Memory.");
        }
        
        // Check embedding generation retrieval settings
        if (string.IsNullOrEmpty(_memoryConfiguration.Retrieval.EmbeddingGeneratorType))
        {
            if (!throwOnError) { return false; }
            throw new ConfigurationException("Retrieval embedding generation (Retrieval.EmbeddingGeneratorType) is not configured in Kernel Memory.");
        }

        return true;
    }

    /// <summary>
    /// Rewrite configuration using OpenAI, if possible.
    /// </summary>
    private void SetupForOpenAI()
    {
        var openAIKey = Environment.GetEnvironmentVariable(OpenAIEnvVar)?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(openAIKey))
            return;

        var inMemoryConfig = new Dictionary<string, string?>
        {
            { $"{ConfigRoot}:Services:OpenAI:APIKey", openAIKey },
            { $"{ConfigRoot}:TextGeneratorType", "OpenAI" },
            { $"{ConfigRoot}:DataIngestion:EmbeddingGeneratorTypes:0", "OpenAI" },
            { $"{ConfigRoot}:Retrieval:EmbeddingGeneratorType", "OpenAI" }
        };
        
        var newAppSettings = new ConfigurationBuilder();
        newAppSettings.AddConfiguration(_rawAppSettings);
        newAppSettings.AddInMemoryCollection(inMemoryConfig);

        _rawAppSettings = newAppSettings.Build();
        _memoryConfiguration = _rawAppSettings.GetSection(ConfigRoot).Get<KernelMemoryConfig>()!;
    }
    
    private static IConfiguration ReadAppSettings(string? settingsDirectory)
    {
        var builder = new ConfigurationBuilder();
        builder.AddKernelMemoryConfigurationSources(settingsDirectory: settingsDirectory);
        return builder.Build();
    }
    
    private IKernelMemoryBuilder BuildUsingConfiguration(IKernelMemoryBuilder builder)
    {
        if (_memoryConfiguration is null)
            throw new ConfigurationException("The given memory configuration is NULL");

        if (_rawAppSettings == null)
            throw new ConfigurationException("The given app settings configuration is NULL");

        // Required by constructors expecting KernelMemoryConfig via DI
        builder.AddSingleton(_memoryConfiguration);

        builder.WithDefaultMimeTypeDetection();
        ConfigureTextPartitioning(builder);
        ConfigureQueueDependency(builder);
        ConfigureStorageDependency(builder);

        // The ingestion embedding generators is a list of generators that the "gen_embeddings" handler uses,
        // to generate embeddings for each partition. While it's possible to use multiple generators (e.g. to compare embedding quality)
        // only one generator is used when searching by similarity, and the generator used for search is not in this list.
        // - config.DataIngestion.EmbeddingGeneratorTypes => list of generators, embeddings to generate and store in memory DB
        // - config.Retrieval.EmbeddingGeneratorType      => one embedding generator, used to search, and usually injected into Memory DB constructor

        ConfigureIngestionEmbeddingGenerators(builder);
        builder.WithSearchClientConfig(_memoryConfiguration.Retrieval.SearchClient);
        ConfigureRetrievalEmbeddingGenerator(builder);

        // The ingestion Memory DBs is a list of DBs where handlers write records to. While it's possible
        // to write to multiple DBs, e.g. for replication purpose, there is only one Memory DB used to
        // read/search, and it doesn't come from this list. See "config.Retrieval.MemoryDbType".
        // Note: use the aux service collection to avoid mixing ingestion and retrieval dependencies.

        ConfigureIngestionMemoryDb(builder);
        ConfigureRetrievalMemoryDb(builder);
        ConfigureTextGenerator(builder);
        ConfigureImageOCR(builder);

        return builder;
    }
    
    private void ConfigureTextPartitioning(IKernelMemoryBuilder builder)
    {
        _memoryConfiguration.DataIngestion.TextPartitioning.Validate();
        builder.WithCustomTextPartitioningOptions(_memoryConfiguration.DataIngestion.TextPartitioning);
    }
    
    private void ConfigureQueueDependency(IKernelMemoryBuilder builder)
    {
        if (!string.Equals(_memoryConfiguration.DataIngestion.OrchestrationType, "Distributed", StringComparison.OrdinalIgnoreCase)) return;
        
        switch (_memoryConfiguration.DataIngestion.DistributedOrchestration.QueueType)
        {
            case  { } y1 when y1.Equals("AzureQueue", StringComparison.OrdinalIgnoreCase):
            case  { } y2 when y2.Equals("AzureQueues", StringComparison.OrdinalIgnoreCase):
                // Check 2 keys for backward compatibility
                builder.Services.AddAzureQueuesOrchestration(GetServiceConfig<AzureQueuesConfig>("AzureQueue") ?? GetServiceConfig<AzureQueuesConfig>("AzureQueues") ?? throw new InvalidOperationException());
                break;

            case  { } y when y.Equals("RabbitMQ", StringComparison.OrdinalIgnoreCase):
                // Check 2 keys for backward compatibility
                builder.Services.AddRabbitMQOrchestration(GetServiceConfig<RabbitMqConfig>("RabbitMq") ?? throw new InvalidOperationException());
                break;

            case  { } y when y.Equals("SimpleQueues", StringComparison.OrdinalIgnoreCase):
                builder.Services.AddSimpleQueues(GetServiceConfig<SimpleQueuesConfig>("SimpleQueues") ?? throw new InvalidOperationException());
                break;
        }
    }
    
    private void ConfigureStorageDependency(IKernelMemoryBuilder builder)
    {
        switch (_memoryConfiguration.DocumentStorageType)
        {
            case  { } x1 when x1.Equals("AzureBlob", StringComparison.OrdinalIgnoreCase):
            case  { } x2 when x2.Equals("AzureBlobs", StringComparison.OrdinalIgnoreCase):
                // Check 2 keys for backward compatibility
                builder.WithAzureBlobsDocumentStorage((GetServiceConfig<AzureBlobsConfig>("AzureBlobs") ?? GetServiceConfig<AzureBlobsConfig>("AzureBlob")) ?? throw new InvalidOperationException());
                break;

            case  { } x when x.Equals("SimpleFileStorage", StringComparison.OrdinalIgnoreCase):
                builder.WithSimpleFileStorage(this.GetServiceConfig<SimpleFileStorageConfig>("SimpleFileStorage"));
                break;
        }
    }
    
    private void ConfigureIngestionEmbeddingGenerators(IKernelMemoryBuilder builder)
    {
        // Note: using multiple embeddings is not fully supported yet and could cause write errors or incorrect search results
        if (_memoryConfiguration.DataIngestion.EmbeddingGeneratorTypes.Count > 1)
            throw new NotSupportedException("""
                                            Using multiple embedding generators is currently unsupported.
                                            You may contact the team if this feature is required, or workaround this exception using KernelMemoryBuilder methods explicitly.
                                            """);

        foreach (var type in this._memoryConfiguration.DataIngestion.EmbeddingGeneratorTypes)
        {
            switch (type)
            {
                case not null when type.Equals("AzureOpenAI", StringComparison.OrdinalIgnoreCase):
                case not null when type.Equals("AzureOpenAIEmbedding", StringComparison.OrdinalIgnoreCase):
                    var azureInstance = GetServiceInstance<ITextEmbeddingGenerator>(builder,
                        s => s.AddAzureOpenAIEmbeddingGeneration(GetServiceConfig<AzureOpenAIConfig>("AzureOpenAIEmbedding")));
                    builder.AddIngestionEmbeddingGenerator(azureInstance);
                    break;

                case not null when type.Equals("OpenAI", StringComparison.OrdinalIgnoreCase):
                    var openAIInstance = GetServiceInstance<ITextEmbeddingGenerator>(builder,
                        s => s.AddOpenAITextEmbeddingGeneration(GetServiceConfig<OpenAIConfig>("OpenAI") ?? throw new InvalidOperationException()));
                    builder.AddIngestionEmbeddingGenerator(openAIInstance);
                    break;
            }
        }
    }
    
    private void ConfigureRetrievalEmbeddingGenerator(IKernelMemoryBuilder builder)
    {
        // Retrieval embeddings - ITextEmbeddingGeneration interface
        switch (_memoryConfiguration.Retrieval.EmbeddingGeneratorType)
        {
            case { } x when x.Equals("AzureOpenAI", StringComparison.OrdinalIgnoreCase):
            case { } y when y.Equals("AzureOpenAIEmbedding", StringComparison.OrdinalIgnoreCase):
                builder.Services.AddAzureOpenAIEmbeddingGeneration(GetServiceConfig<AzureOpenAIConfig>("AzureOpenAIEmbedding"));
                break;

            case { } x when x.Equals("OpenAI", StringComparison.OrdinalIgnoreCase):
                builder.Services.AddOpenAITextEmbeddingGeneration(GetServiceConfig<OpenAIConfig>("OpenAI") ?? throw new InvalidOperationException());
                break;
        }
    }
    
    private void ConfigureIngestionMemoryDb(IKernelMemoryBuilder builder)
    {
        foreach (var type in this._memoryConfiguration.DataIngestion.MemoryDbTypes)
        {
            switch (type)
            {
                case not null when type.Equals("AzureAISearch", StringComparison.OrdinalIgnoreCase):
                {
                    var instance = GetServiceInstance<IMemoryDb>(builder,
                        s => s.AddAzureAISearchAsMemoryDb(GetServiceConfig<AzureAISearchConfig>("AzureAISearch") ?? throw new InvalidOperationException()));
                    builder.AddIngestionMemoryDb(instance);
                    break;
                }

                case not null when type.Equals("Qdrant", StringComparison.OrdinalIgnoreCase):
                {
                    var instance = GetServiceInstance<IMemoryDb>(builder, s => s.AddQdrantAsMemoryDb(GetServiceConfig<QdrantConfig>("Qdrant") ?? throw new InvalidOperationException()));
                    builder.AddIngestionMemoryDb(instance);
                    break;
                }

                case not null when type.Equals("Postgres", StringComparison.OrdinalIgnoreCase):
                {
                    var instance = GetServiceInstance<IMemoryDb>(builder, s => s.AddPostgresAsMemoryDb(GetServiceConfig<PostgresConfig>("Postgres") ?? throw new InvalidOperationException()));
                    builder.AddIngestionMemoryDb(instance);
                    break;
                }

                case not null when type.Equals("Redis", StringComparison.OrdinalIgnoreCase):
                {
                    var instance = GetServiceInstance<IMemoryDb>(builder, s => s.AddRedisAsMemoryDb(GetServiceConfig<RedisConfig>("Redis") ?? throw new InvalidOperationException())
                    );
                    builder.AddIngestionMemoryDb(instance);
                    break;
                }

                case not null when type.Equals("SimpleVectorDb", StringComparison.OrdinalIgnoreCase):
                {
                    var instance = GetServiceInstance<IMemoryDb>(builder,s => s.AddSimpleVectorDbAsMemoryDb(GetServiceConfig<SimpleVectorDbConfig>("SimpleVectorDb")));
                    builder.AddIngestionMemoryDb(instance);
                    break;
                }

                case not null when type.Equals("SimpleTextDb", StringComparison.OrdinalIgnoreCase):
                {
                    var instance = GetServiceInstance<IMemoryDb>(builder, s => s.AddSimpleTextDbAsMemoryDb(GetServiceConfig<SimpleTextDbConfig>("SimpleTextDb")));
                    builder.AddIngestionMemoryDb(instance);
                    break;
                }
                
                case "":
                    // NOOP - allow custom implementations, via WithCustomMemoryDb()
                    break;
                
                default:
                    throw new ConfigurationException(
                        $"Unknown Memory DB option '{type}'. " +
                        "To use a custom Memory DB, set the configuration value to an empty string, " +
                        "and inject the custom implementation using `IKernelMemoryBuilder.WithCustomMemoryDb(...)`");
            }
        }
    }
    
    private void ConfigureRetrievalMemoryDb(IKernelMemoryBuilder builder)
    {
        // Retrieval Memory DB - IMemoryDb interface
        switch (_memoryConfiguration.Retrieval.MemoryDbType)
        {
            case { } x when x.Equals("AzureAISearch", StringComparison.OrdinalIgnoreCase):
                builder.Services.AddAzureAISearchAsMemoryDb(GetServiceConfig<AzureAISearchConfig>("AzureAISearch") ?? throw new InvalidOperationException());
                break;

            case { } x when x.Equals("Qdrant", StringComparison.OrdinalIgnoreCase):
                builder.Services.AddQdrantAsMemoryDb(GetServiceConfig<QdrantConfig>("Qdrant") ?? throw new InvalidOperationException());
                break;

            case { } x when x.Equals("Postgres", StringComparison.OrdinalIgnoreCase):
                builder.Services.AddPostgresAsMemoryDb(GetServiceConfig<PostgresConfig>("Postgres") ?? throw new InvalidOperationException());
                break;

            case { } x when x.Equals("Redis", StringComparison.OrdinalIgnoreCase):
                builder.Services.AddRedisAsMemoryDb(GetServiceConfig<RedisConfig>("Redis") ?? throw new InvalidOperationException());
                break;

            case { } x when x.Equals("SimpleVectorDb", StringComparison.OrdinalIgnoreCase):
                builder.Services.AddSimpleVectorDbAsMemoryDb(GetServiceConfig<SimpleVectorDbConfig>("SimpleVectorDb"));
                break;

            case { } x when x.Equals("SimpleTextDb", StringComparison.OrdinalIgnoreCase):
                builder.Services.AddSimpleTextDbAsMemoryDb(GetServiceConfig<SimpleTextDbConfig>("SimpleTextDb"));
                break;
        }
    }
    
    private void ConfigureTextGenerator(IKernelMemoryBuilder builder)
    {
        // Text generation
        switch (_memoryConfiguration.TextGeneratorType)
        {
            case { } x when x.Equals("AzureOpenAI", StringComparison.OrdinalIgnoreCase):
            case { } y when y.Equals("AzureOpenAIText", StringComparison.OrdinalIgnoreCase):
                builder.Services.AddAzureOpenAITextGeneration(GetServiceConfig<AzureOpenAIConfig>("AzureOpenAIText"));
                break;

            case { } x when x.Equals("OpenAI", StringComparison.OrdinalIgnoreCase):
                builder.Services.AddOpenAITextGeneration(GetServiceConfig<OpenAIConfig>("OpenAI") ?? throw new InvalidOperationException());
                break;
        }
    }
    
    private void ConfigureImageOCR(IKernelMemoryBuilder builder)
    {
        // Image OCR
        switch (_memoryConfiguration.DataIngestion.ImageOcrType)
        {
            case { } y when string.IsNullOrWhiteSpace(y):
            case { } x when x.Equals("None", StringComparison.OrdinalIgnoreCase):
                break;

            case { } x when x.Equals("AzureAIDocIntel", StringComparison.OrdinalIgnoreCase):
                builder.Services.AddAzureAIDocIntel(GetServiceConfig<AzureAIDocIntelConfig>("AzureAIDocIntel") ?? throw new InvalidOperationException());
                break;
        }
    }
    
    /// <summary>
    /// Get an instance of T, using dependencies available in the builder, except for existing service descriptors for T.
    /// Replace/Use the given action to define T's implementation. Return an instance of T built using the definition provided by the action.
    /// </summary>
    /// <param name="builder">Kernel memory builder</param>
    /// <param name="addCustomService">Action used to configure the service collection</param>
    /// <typeparam name="T">Target type/interface</typeparam>
    private static T GetServiceInstance<T>(IKernelMemoryBuilder builder, Action<IServiceCollection> addCustomService)
    {
        // Clone the list of service descriptors, skipping T descriptor
        var services = new ServiceCollection() as IServiceCollection;
        foreach (var d in builder.Services)
        {
            if (d.ServiceType == typeof(T)) { continue; }
            services.Add(d);
        }

        // Add the custom T descriptor
        addCustomService.Invoke(services);

        // Build and return an instance of T, as defined by `addCustomService`
        return services.BuildServiceProvider().GetService<T>() ?? throw new ConfigurationException($"Unable to build {nameof(T)}");
    }
    
    /// <summary>
    /// Read a dependency configuration from IConfiguration
    /// Data is usually retrieved from KernelMemory:Services:{serviceName}, e.g. when using appsettings.json
    /// {
    ///   "KernelMemory": {
    ///     "Services": {
    ///       "{serviceName}": {
    ///         ...
    ///         ...
    ///       }
    ///     }
    ///   }
    /// }
    /// </summary>
    /// <param name="serviceName">Name of the dependency</param>
    /// <typeparam name="T">Type of configuration to return</typeparam>
    /// <returns>Configuration instance, settings for the dependency specified</returns>
    private T? GetServiceConfig<T>(string serviceName) => _memoryConfiguration.GetServiceConfig<T>(_rawAppSettings, serviceName);
}
