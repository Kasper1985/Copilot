using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using WebApi.Controllers;
using WebApi.Hubs;
using WebApi.Options;
using WebApi.Services.Interfaces;

namespace WebApi.Services;

/// <summary>
/// Middleware for determining is site is undergoing maintenance.
/// </summary>
public class MaintenanceMiddleware(RequestDelegate next, IReadOnlyList<IMaintenanceAction> actions, IOptions<ServiceOptions> serviceOptions, IHubContext<MessageRelayHub> messageRelayHubContext,
    ILogger<MaintenanceMiddleware> logger)
{
    private bool? _isInMaintenance;
    
    public async Task Invoke(HttpContext ctx, Kernel kernel)
    {
        // Skip inspection if _isInMaintenance explicitly false.
        if (_isInMaintenance ?? true)
        {
            // Maintenance never false => true; always true => false or just false;
            _isInMaintenance = await InspectMaintenanceActionAsync();
        }

        // In maintenance if actions say so or explicitly configured.
        if (serviceOptions.Value.InMaintenance)
            await messageRelayHubContext.Clients.All.SendAsync(MaintenanceController.GlobalSiteMaintenance, "Site undergoing maintenance...");

        await next(ctx);
    }

    private async Task<bool> InspectMaintenanceActionAsync()
    {
        var inMaintenance = false;
        foreach (var action in actions)
            inMaintenance |= await action.InvokeAsync();

        return inMaintenance;
    }
}
