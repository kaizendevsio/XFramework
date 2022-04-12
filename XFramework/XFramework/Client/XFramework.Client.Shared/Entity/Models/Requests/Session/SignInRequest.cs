namespace XFramework.Client.Shared.Entity.Models.Requests.Session;

public class SignInRequest
{
    public string Username { get; set; }
    public string Password { get; set; }
    public string MobileNumber { get; set; }
    public bool Remember { get; set; }
}