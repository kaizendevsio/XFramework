namespace XFramework.Client.Shared.Entity.Models.Requests;

public class SignInRequest
{
    public string Email { get; set; }
    public string Password { get; set; }
    public string IsSuccess { get; set; } = "Nope";
}