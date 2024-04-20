namespace XFramework.Domain.Shared.BusinessObjects;

public class JwtOptions
{
    public string Secret { get; set; }
    public string ValidIssuer { get; set; }
    public string ValidAudience { get; set; }
    public string AccessTokenLifespan  { get; set; }
    public string RefreshTokenLifespan  { get; set; }
}