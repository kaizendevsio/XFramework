namespace XFramework.Client.Shared.Core.Features.Application;

public partial class ApplicationState : State<ApplicationState>
{
    public override void Initialize()
    {
    }

    public bool IsBusy { get; set; }
    public bool NoSpinner { get; set; }
    public string ProgressTitle { get; set; }
    public string ProgressMessage { get; set; }
    public bool StateRestored { get; set; }
    public int NotificationCount { get; set; }
       
}