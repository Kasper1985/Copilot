namespace WebApi.Extensions;

internal static class ConfigurationExtensions
{
    public static IHostBuilder AddConfigurations(this IHostBuilder builder)
    {
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");

        builder.ConfigureAppConfiguration((builderContext, configBuilder) =>
        {
            configBuilder.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
            configBuilder.AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: true);

            configBuilder.AddEnvironmentVariables();
        });
            
        return builder;
    }
}
