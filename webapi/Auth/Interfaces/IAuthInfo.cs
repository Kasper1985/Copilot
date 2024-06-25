namespace WebApi.Auth.Interfaces;

public interface IAuthInfo
{
    /// <summary>
    /// the authenticated user's unique ID.
    /// </summary>
    public string UserId { get; }
    
    /// <summary>
    /// The authenticated user's name.
    /// </summary>
    public string Name { get; }
}
