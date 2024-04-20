namespace XFramework.Domain.Shared.BusinessObjects;

public class JwtToken
{
    public string AccessToken  { get; set; }
    public string RefreshToken  { get; set; }
}