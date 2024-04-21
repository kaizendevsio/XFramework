namespace XFramework.Blazor.Entity.Models.Requests.Session;

public class SignInRequest
{
    public string Username { get; set; }
    public string Password { get; set; }
    public bool Remember { get; set; }
}