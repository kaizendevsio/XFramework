namespace XFramework.Client.Shared.Core.Features.Session;

public partial class SessionState
{
    public class SmsVerificationAction : IAction
    {
        public SmsVerificationRequest Request { get; set; }
        public string  NavigateTo { get; set; }
    }
}

