namespace XFramework.Domain.Generic.Contracts.Responses;

public class AuthenticationResponse
{
    public string IdToken { get; set; }
    public string AccessToken { get; set; }
    public string TokenType { get; set; }
    public int ExpiresIn { get; set; }
    public string RefreshToken { get; set; }
}