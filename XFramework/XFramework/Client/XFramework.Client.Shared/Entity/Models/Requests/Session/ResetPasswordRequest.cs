namespace XFramework.Client.Shared.Entity.Models.Requests.Session;

public class ResetPasswordRequest
{
    public string? PhoneEmailUsername { get; set; }
    public string? Password { get; set; }
    public string? PasswordConfirmation { get; set; }
}