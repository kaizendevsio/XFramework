namespace XFramework.Domain.Shared.BusinessObjects;

public class JwtToken
{
    public string AccessToken { get; set; } = default!;
    public string RefreshToken { get; set; } = default!;
    public Guid SessionId { get; set; }
}