namespace XFramework.Client.Shared.Entity.Models.Requests;

public class SignInRequest
{
    public string Username { get; set; }
    public string Password { get; set; }
    public bool Remember { get; set; }
}