namespace XFramework.Client.Shared.Core.Features.Application;

public partial class ApplicationState
{
    public class SetState : IAction
    {
        public bool? IsBusy { get; set; }
        public bool? NoSpinner { get; set; }
        public string ProgressTitle { get; set; }
        public string ProgressMessage { get; set; } = string.Empty;
        public bool? StateRestored { get; set; }
        public int? NotificationCount { get; set; }

    }
}