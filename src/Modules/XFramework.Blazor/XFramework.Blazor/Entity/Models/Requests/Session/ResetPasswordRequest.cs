namespace XFramework.Blazor.Entity.Models.Requests.Session;

public class ResetPasswordRequest
{
    public string? PhoneEmailUsername { get; set; }
    public string? Password { get; set; }
    public string? PasswordConfirmation { get; set; }
}