using WebApi.Auth.Interfaces;

namespace WebApi.Auth;

public class AuthInfo (string userId, string userName) : IAuthInfo
{
    private static readonly string DefaultUserId = "c05c61eb-65e4-4223-915a-fe72b0c9ece1";
    private static readonly string DefaultUserName = "Default user";
    
    private record struct AuthData(string UserId, string UserName);
    
    private readonly Lazy<AuthData> _data = new (() => new AuthData(userId, userName), isThreadSafe: false);
    
    public AuthInfo() : this(DefaultUserId, DefaultUserName) { }

    /// <inheridoc />
    public string UserId => _data.Value.UserId;

    /// <inheridoc />
    public string Name => _data.Value.UserName;
}
