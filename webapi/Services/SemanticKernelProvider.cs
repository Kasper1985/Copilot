using Microsoft.Extensions.Options;
using Microsoft.KernelMemory;
using Microsoft.SemanticKernel;

namespace WebApi.Services;

/// <summary>
/// Extension methods for registering Semantic Kernel related services.
/// </summary>
public class SemanticKernelProvider(IServiceProvider serviceProvider, IConfiguration configuration, IHttpClientFactory httpClientFactory)
{
    private readonly IKernelBuilder _builderChat = InitializeCompletionKernel(serviceProvider, configuration, httpClientFactory);

    /// <summary>
    /// Produce semantic-kernel with only completion services for chat.
    /// </summary>
    /// <returns></returns>
    public Kernel GetCompletionKernel() => _builderChat.Build();
    
    private static IKernelBuilder InitializeCompletionKernel(IServiceProvider serviceProvider, IConfiguration configuration, IHttpClientFactory httpClientFactory)
    {
        var builder = Kernel.CreateBuilder();

        builder.Services.AddLogging();

        var memoryOptions = serviceProvider.GetRequiredService<IOptions<KernelMemoryConfig>>().Value;
        switch (memoryOptions.TextGeneratorType)
        {
            case { } x when x.Equals("AzureOpenAI", StringComparison.OrdinalIgnoreCase):
            case { } y when y.Equals("AzureOpenAIText", StringComparison.OrdinalIgnoreCase):
                var azureAIOptions = memoryOptions.GetServiceConfig<AzureOpenAIConfig>(configuration, "AzureOpenAIText");
                builder.AddAzureOpenAIChatCompletion(azureAIOptions.Deployment, azureAIOptions.Endpoint, azureAIOptions.APIKey, httpClient: httpClientFactory.CreateClient());
                break;

            case { } x when x.Equals("OpenAI", StringComparison.OrdinalIgnoreCase):
                var openAIOptions = memoryOptions.GetServiceConfig<OpenAIConfig>(configuration, "OpenAI");
                builder.AddOpenAIChatCompletion(openAIOptions.TextModel, openAIOptions.APIKey, httpClient: httpClientFactory.CreateClient());
                break;

            default:
                throw new ArgumentException($"Invalid {nameof(memoryOptions.TextGeneratorType)} value in 'KernelMemory' settings.");
        }

        return builder;
    }
}
