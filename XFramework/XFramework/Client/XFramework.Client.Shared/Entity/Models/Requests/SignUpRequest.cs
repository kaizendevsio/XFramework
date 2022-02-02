namespace XFramework.Client.Shared.Entity.Models.Requests;

public class SignUpRequest
{
    public string RegisterEmail { get; set; }
    public string RegisterMobile { get; set; }
    public string RegisterPassword { get; set; }
    public string RegisterPasswordAgain { get; set; }
}