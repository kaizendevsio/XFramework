namespace XFramework.Client.Shared.Core.Features.Configuration;

public partial class ConfigurationState
{
    public class SetState : IAction
    {
        public ConfigurationModel Configuration { get; set; }
    }
}