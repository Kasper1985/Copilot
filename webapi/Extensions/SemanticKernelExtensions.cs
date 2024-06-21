using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using Microsoft.KernelMemory;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Plugins.Core;
using WebApi.Enums;
using WebApi.Hubs;
using WebApi.Models.Response;
using WebApi.Options;
using WebApi.Plugins.Chat;
using WebApi.Services;
using WebApi.Services.Interfaces;
using WebApi.Storage.Repositories;

namespace WebApi.Extensions;

/// <summary>
/// Extension methods for registering Semantic Kernel related services.
/// </summary>
internal static class SemanticKernelExtensions
{
    /// <summary>
    /// Delegate to register function with a Semantic Kernel.
    /// </summary>
    public delegate Task RegisterFunctionsWithKernel(IServiceProvider sp, Kernel kernel);

    /// <summary>
    /// Delegate for any complimentary setup of the kernel, i.e. registering custom plugins, etc.
    /// </summary>
    public delegate Task KernelSetupHook(IServiceProvider sp, Kernel kernel);
    
    
    /// <summary>
    /// Adds embedding model.
    /// </summary>
    internal static WebApplicationBuilder AddBotConfig(this WebApplicationBuilder builder)
    {
        builder.Services.AddScoped(sp => sp.WithBotConfig(builder.Configuration));
        return builder;
    }
    
    internal static WebApplicationBuilder AddSemanticKernelServices(this WebApplicationBuilder builder)
    {
        builder.InitializeKernelProvider();
        
        // Semantic Kernel
        builder.Services.AddScoped<Kernel>(sp =>
        {
            var provider = sp.GetRequiredService<SemanticKernelProvider>();
            var kernel = provider.GetCompletionKernel();
            
            sp.GetRequiredService<RegisterFunctionsWithKernel>()(sp, kernel);
            
            // If KernelSetupHook is not null, invoke custom kernel setup.
            sp.GetService<KernelSetupHook>()?.Invoke(sp, kernel);
            return kernel;
        });
        
        // Azure Content Safety
        builder.Services.AddContentSafety();
        
        // Register plugins
        builder.Services.AddScoped<RegisterFunctionsWithKernel>(_ => RegisterChatCopilotFunctions);
        
        // Place to add any additional steps needed for the kernel.
        // Uncomment the following line and pass in a custom hook for any complimentary setup of the kernel.
        // builder.Services.AddKernelSetupHook(customHook);
        
        return builder;
    }

    /// <summary>
    /// Register the chat plugin with the kernel.
    /// </summary>
    private static Kernel RegisterChatPlugin(this Kernel kernel, IServiceProvider sp)
    {
        kernel.ImportPluginFromObject(new ChatPlugin(
            kernel, 
            memoryClient: sp.GetRequiredService<IKernelMemory>(),
            chatMessageRepository: sp.GetRequiredService<CopilotChatMessageRepository>(),
            chatSessionRepository: sp.GetRequiredService<ChatSessionRepository>(),
            messageRelayHubContext: sp.GetRequiredService<IHubContext<MessageRelayHub>>(),
            promptOptions: sp.GetRequiredService<IOptions<PromptsOptions>>(),
            documentImportOptions: sp.GetRequiredService<IOptions<DocumentMemoryOptions>>(),
            contentSafety: sp.GetService<AzureContentSafety>(),
            logger: sp.GetRequiredService<ILogger<ChatPlugin>>()),
            nameof(ChatPlugin));
        
        return kernel;
    }
    
    /// <summary>
    /// Gets the embedding model from the configuration.
    /// </summary>
    private static ChatArchiveEmbeddingConfig WithBotConfig(this IServiceProvider provider, IConfiguration configuration)
    {
        var memoryOptions = provider.GetRequiredService<IOptions<KernelMemoryConfig>>().Value;

        switch (memoryOptions.Retrieval.EmbeddingGeneratorType)
        {
            case { } x when x.Equals("AzureOpenAI", StringComparison.OrdinalIgnoreCase):
            case { } y when y.Equals("AzureOpenAIEmbedding", StringComparison.OrdinalIgnoreCase):
                var azureAIOptions = memoryOptions.GetServiceConfig<AzureOpenAIConfig>(configuration, "AzureOpenAIEmbedding");
                return new ChatArchiveEmbeddingConfig
                {
                    AIService = AIServiceType.AzureOpenAIEmbedding,
                    DeploymentOrModelId = azureAIOptions.Deployment
                };
            
            case { } x when x.Equals("OpenAI", StringComparison.OrdinalIgnoreCase):
                var openAIOptions = memoryOptions.GetServiceConfig<OpenAIConfig>(configuration, "OpenAI");
                return new ChatArchiveEmbeddingConfig
                {
                    AIService = AIServiceType.OpenAI,
                    DeploymentOrModelId = openAIOptions.EmbeddingModel
                };
            
            default:
                throw new ArgumentException($"Invalid {nameof(memoryOptions.Retrieval.EmbeddingGeneratorType)} value in 'SemanticMemory' settings.");
        }
    }

    private static void InitializeKernelProvider(this WebApplicationBuilder builder)
    {
        builder.Services.AddSingleton(sp => new SemanticKernelProvider(sp, builder.Configuration, sp.GetRequiredService<IHttpClientFactory>()));
    }

    /// <summary>
    /// Adds Azure Content Safety.
    /// </summary>
    /// <param name="services"></param>
    private static void AddContentSafety(this IServiceCollection services)
    {
        var configuration = services.BuildServiceProvider().GetRequiredService<IConfiguration>();
        var options = configuration.GetSection(ContentSafetyOptions.PropertyName).Get<ContentSafetyOptions>() ?? new ContentSafetyOptions { Enabled = false };
        services.AddSingleton<IContentSafetyService>(_ => new AzureContentSafety(options.Endpoint, options.Key));
    }
    
    /// <summary>
    /// Register functions with the main kernel responsible for handling Chat Copilot requests.
    /// </summary>
    private static Task RegisterChatCopilotFunctions(IServiceProvider sp, Kernel kernel)
    {
        // Chat Copilot functions
        kernel.RegisterChatPlugin(sp);
        
        // Time plugin
#pragma warning disable SKEXP0050
        kernel.ImportPluginFromObject(new TimePlugin(), nameof(TimePlugin));
#pragma warning restore SKEXP0050
        
        return Task.CompletedTask;
    }
    
    /// <summary>
    /// Register custom hook for any complimentary setup of the kernel.
    /// </summary>
    /// <param name="services"><see cref="IServiceCollection"/> of services.</param>
    /// <param name="hook">The delegate to perform any additional setup of the kernel.</param>
    private static IServiceCollection AddKernelSetupHook(this IServiceCollection services, KernelSetupHook hook)
    {
        // Add the hook to the service collection
        services.AddSingleton<KernelSetupHook>(_ => hook);
        return services;
    }
}
