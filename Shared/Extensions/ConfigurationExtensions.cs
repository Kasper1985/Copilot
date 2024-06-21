using Microsoft.Extensions.Configuration;
using Microsoft.KernelMemory.DataFormats;
using Shared.Tesseract;

namespace Shared.Extensions;

/// <summary>
/// Dependency injection for kernel memory using configuration defined in appsettings.json
/// </summary>
public static class ConfigurationExtensions
{
    private const string ConfigOcrType = "ImageOcrType";

    public static IOcrEngine? CreateCustomOcr(this IConfiguration configuration)
    {
        var ocrType = configuration.GetSection($"{MemoryConfiguration.KernelMemorySection}:{ConfigOcrType}").Value ?? string.Empty;
        switch (ocrType)
        {
            case not null when ocrType.Equals(TesseractOptions.SectionName, StringComparison.OrdinalIgnoreCase):
                var tesseractOptions = configuration
                        .GetSection($"{MemoryConfiguration.KernelMemorySection}:{MemoryConfiguration.ServicesSection}:{TesseractOptions.SectionName}")
                        .Get<TesseractOptions>();

                if (tesseractOptions is null)
                    throw new ArgumentNullException($"Missing configuration for {ConfigOcrType}: {ocrType}");

                return new TesseractOcrEngine(tesseractOptions);
        }

        return null;
    }
}
