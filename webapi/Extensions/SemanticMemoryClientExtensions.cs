using Microsoft.Extensions.Options;
using Microsoft.KernelMemory;
using Shared;
using Shared.Extensions;
using WebApi.Models.Storage;
using WebApi.Services;

namespace WebApi.Extensions;

/// <summary>
/// Extension methods for <see cref="IKernelMemory"/> and service registration.
/// </summary>
public static class SemanticMemoryClientExtensions
{
    private static readonly List<string> PipelineSteps = ["extract", "partition", "gen_embeddings", "save_records"];

    /// <summary>
    /// Inject <see cref="IKernelMemory"/>.
    /// </summary>
    /// <param name="appBuilder"></param>
    public static void AddSemanticMemoryServices(this WebApplicationBuilder appBuilder)
    {
        var serviceProvider = appBuilder.Services.BuildServiceProvider();

        var memoryConfig = serviceProvider.GetRequiredService<IOptions<KernelMemoryConfig>>().Value;
        
        var ocrType = memoryConfig.DataIngestion.ImageOcrType;
        var hasOcr = !string.IsNullOrWhiteSpace(ocrType) && !ocrType.Equals(MemoryConfiguration.NoneType, StringComparison.OrdinalIgnoreCase);
        
        var pipelineType = memoryConfig.DataIngestion.OrchestrationType;
        var isDistributed = pipelineType.Equals(MemoryConfiguration.OrchestrationTypeDistributed, StringComparison.OrdinalIgnoreCase);
        
        appBuilder.Services.AddSingleton(_ => new DocumentTypeProvider(hasOcr));
        
        var memoryBuilder = new KernelMemoryBuilder(appBuilder.Services);
        if (isDistributed)
            memoryBuilder.WithoutDefaultHandlers();
        else if (hasOcr)
            memoryBuilder.WithCustomOcr(appBuilder.Configuration);
        
        var memory = memoryBuilder.FromMemoryConfiguration(memoryConfig, appBuilder.Configuration).Build();
        
        appBuilder.Services.AddSingleton(memory);
    }

    public static Task<SearchResult> SearchMemory(this IKernelMemory memoryClient, string indexName, string query, float relevanceThreshold, string chatId, string? memoryName = null,
        CancellationToken cancellationToken = default) =>
        memoryClient.SearchMemory(indexName, query, relevanceThreshold, resultCount: -1, chatId, memoryName, cancellationToken);

    public static async Task<SearchResult> SearchMemory(this IKernelMemory memoryClient, string indexName, string query, float relevanceThreshold, int resultCount, string chatId, string? memoryName = null,
        CancellationToken cancellationToken = default)
    {
        var filter = new MemoryFilter();
        filter.ByTag(MemoryTags.TagChatId, chatId);
        
        if (!string.IsNullOrWhiteSpace(memoryName))
            filter.ByTag(MemoryTags.TagMemory, memoryName);
        
        return await memoryClient.SearchAsync(query, indexName, filter, null, relevanceThreshold, resultCount, cancellationToken: cancellationToken);
    }
    
    public static Task StoreMemory(this IKernelMemory memoryClient, string indexName, string chatId, string memoryName, string memory, CancellationToken cancellationToken = default) =>
        memoryClient.StoreMemory(indexName, chatId, memoryName, memoryId: Guid.NewGuid().ToString(), memory, cancellationToken);

    public static async Task StoreMemory(this IKernelMemory memoryClient, string indexName, string chatId, string memoryName, string memoryId, string memory,
        CancellationToken cancellationToken = default)
    {
        using var stream = new MemoryStream();
        await using var writer = new StreamWriter(stream);
        await writer.WriteAsync(memory);
        await writer.FlushAsync(cancellationToken);
        stream.Position = 0;

        var uploadRequest = new DocumentUploadRequest
        {
            DocumentId = memoryId,
            Index = indexName,
            Files = [new DocumentUploadRequest.UploadedFile("memory.txt", stream)],
            Steps = PipelineSteps
        };
        
        uploadRequest.Tags.Add(MemoryTags.TagChatId, chatId);
        uploadRequest.Tags.Add(MemoryTags.TagMemory, memoryName);
        
        await memoryClient.ImportDocumentAsync(uploadRequest, cancellationToken: cancellationToken);
    }
}
