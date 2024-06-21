using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using WebApi.Options;
using WebApi.Models.Response;

namespace WebApi.Controllers;

/// <summary>
/// Controller for reporting the status of chat migration.
/// </summary>
[ApiController]
public class MaintenanceController(ILogger<MaintenanceController> logger, IOptions<ServiceOptions> serviceOptions) : ControllerBase
{
    internal const string GlobalSiteMaintenance = "GlobalSiteMaintenance";

    /// <summary>
    /// Route for reporting the status of site maintenance.
    /// </summary>
    [Route("maintenanceStatus")]
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public ActionResult<MaintenanceResult?> GetMaintenanceStatusAsync(CancellationToken cancellationToken = default)
    {
        MaintenanceResult? result = null;

        if (serviceOptions.Value.InMaintenance)
            result = new MaintenanceResult(); // Default maintenance message

        if (result != null)
            return Ok(result);

        return Ok();
    }
}
