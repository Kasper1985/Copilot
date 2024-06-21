using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Web;

namespace WebApi.Auth;

/// <summary>
/// Class implementing "authentication" that lets all requests pass through.
/// </summary>
public class PassThroughAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory loggerFactory, UrlEncoder encoder) 
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, loggerFactory, encoder)
{
    private const string AuthenticationScheme = "PassThrough";
    private const string DefaultUserId = "c05c61eb-65e4-4223-915a-fe72b0c9ece1";
    private const string DefaultUserName = "Default User";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        Logger.LogInformation("Allowing request to pass through");
        
        var userIdClaim = new Claim(ClaimConstants.Sub, DefaultUserId);
        var nameClaim = new Claim(ClaimConstants.Name, DefaultUserName);
        var identity = new ClaimsIdentity([userIdClaim, nameClaim], AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);
        
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
    
    /// <summary>
    /// Returns true if the given user ID is the default user guest ID.
    /// </summary>
    public static bool IsDefaultUser(string userId) => userId == DefaultUserId;
}
