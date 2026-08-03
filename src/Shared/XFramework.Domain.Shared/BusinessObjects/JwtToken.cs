namespace XFramework.Domain.Shared.BusinessObjects;

public class JwtToken
{
    public string AccessToken { get; set; } = default!;
    public string RefreshToken { get; set; } = default!;
    public DateTime RefreshTokenExpiresAt { get; set; }
    public Guid SessionId { get; set; }
}
