using Microsoft.Extensions.Configuration;
using Microsoft.KernelMemory;

namespace Shared.Extensions;

/// <summary>
/// Dependency injection for kernel memory using custom OCR configuration defined in appsettings.json
/// </summary>
public static class MemoryClientBuilderExtensions
{
    public static IKernelMemoryBuilder WithCustomOcr(this IKernelMemoryBuilder builder, IConfiguration configuration)
    {
        var ocrEngine = configuration.CreateCustomOcr();

        if (ocrEngine is not null)
            builder.WithCustomImageOcr(ocrEngine);

        return builder;
    }
}
