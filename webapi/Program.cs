using System.Text.Json;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using WebApi.Extensions;
using WebApi.Hubs;
using WebApi.Services;

var builder = WebApplication.CreateBuilder(args);

// Load configurations from appsettings.json and its environment-specific counterpart.
builder.Host.AddConfigurations();
builder.WebHost.UseUrls(); // Disables endpoint override warning message when using IConfiguration for Kestrel endpoint.

// Add configuration options and required services.
builder.Services
    .AddSingleton<ILogger>(sp => sp.GetRequiredService<ILogger<Program>>())
    .AddOptions(builder.Configuration)
    .AddPersistentChatStore();

// Configure and add semantic services.
builder
    .AddBotConfig()
    .AddSemanticKernelServices()
    .AddSemanticMemoryServices();

// Add SignalR as the real-time relay service.
builder.Services.AddSignalR();

// Add named HTTP clients for IHttpClientFactory
builder.Services.AddHttpClient();

builder.Services
    .AddMaintenanceServices()
    .AddEndpointsApiExplorer()
    .AddSwaggerGen()
    .AddCorsPolicy(builder.Configuration)
    .AddControllers()
    .AddJsonOptions(options => { options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase; });
builder.Services.AddHealthChecks();

// Configure middleware and endpoints
var app = builder.Build();
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<MaintenanceMiddleware>();
app.MapControllers()
    .RequireAuthorization();
app.MapHealthChecks("/healthz");

// Add Chat Copilot hub for real time communication
app.MapHub<MessageRelayHub>("/messageRelayHub");

// Enable Swagger for development environments.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    // Redirect root URL to Swagger UI URL
    app.MapWhen(context => context.Request.Path == "/",
        appBuilder => appBuilder.Run(async context => await Task.Run(() => context.Response.Redirect("/swagger"))));
}

// Start the service.
var runTask = app.RunAsync();

// Log the health probe URL for users to validate the service is running.
try
{
    var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()?.Addresses.FirstOrDefault();
    app.Services.GetRequiredService<ILogger>().LogInformation("Health probe: {address}/healthz", address);
}
catch (ObjectDisposedException)
{
    // We likely failed startup which disposes 'app.Services' - don't attempt to display the health probe URL.
}

// Wait for the service to complete.
await runTask;
