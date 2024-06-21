namespace WebApi.Services.Interfaces;

/// <summary>
/// Defines discrete maintenance action responsible for both inspecting state and performing maintenance.
/// </summary>
public interface IMaintenanceAction
{
    /// <summary>
    /// Calling site to initiate maintenance action.
    /// </summary>
    /// <returns>True if maintenance needed or in progress</returns>
    Task<bool> InvokeAsync(CancellationToken cancellation = default);
}
