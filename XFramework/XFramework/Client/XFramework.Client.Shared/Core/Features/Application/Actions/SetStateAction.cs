namespace XFramework.Client.Shared.Core.Features.Application;

public partial class ApplicationState
{
    public class SetState : IAction
    {
        public bool? IsBusy { get; set; }
    }
}