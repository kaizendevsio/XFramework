namespace XFramework.Client.Shared.Entity.Models.Requests.Session;

public class ForgotPasswordRequest
{
    public string Email { get; set; }
    public string PhoneNumber { get; set; }
    public string UserName { get; set; }
    public string Password { get; set; }
}