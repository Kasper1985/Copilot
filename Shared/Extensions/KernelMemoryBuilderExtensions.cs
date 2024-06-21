using Microsoft.Extensions.Configuration;
using Microsoft.KernelMemory;

namespace Shared.Extensions;

/// <summary>
/// Kernel Memory builder extensions for apps using settings in appsettings.json and using IConfiguration.
/// </summary>
public static class KernelMemoryBuilderExtensions
{
    /// <summary>
    /// Configure the builder using settings stored in the specified directory.
    /// If directory is empty, use the current assembly folder.
    /// </summary>
    /// <param name="builder">KernelMemory builder instance.</param>
    /// <param name="settingsDirectory">Directory containing appsettings.json (incl. dev/prod).</param>
    /// <returns></returns>
    public static IKernelMemoryBuilder FromAppSettings(this IKernelMemoryBuilder builder, string? settingsDirectory = null) => new ServiceConfiguration(settingsDirectory).PrepareBuilder(builder);
    
    /// <summary>
    /// Configure the builder using settings from the given KernelMemoryConfig and IConfiguration instances.
    /// </summary>
    /// <param name="builder">KernelMemory builder instance</param>
    /// <param name="memoryConfiguration">Kernel memory configuration</param>
    /// <param name="servicesConfiguration">Dependencies configuration, e.g. queue, embedding, storage, etc.</param>
    public static IKernelMemoryBuilder FromMemoryConfiguration(this IKernelMemoryBuilder builder, KernelMemoryConfig memoryConfiguration, IConfiguration servicesConfiguration) =>
        new ServiceConfiguration(servicesConfiguration, memoryConfiguration).PrepareBuilder(builder);
}
