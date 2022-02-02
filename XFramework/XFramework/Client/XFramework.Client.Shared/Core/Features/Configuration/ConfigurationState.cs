namespace XFramework.Client.Shared.Core.Features.Configuration;

public partial class ConfigurationState : State<ConfigurationState>
{
    public override void Initialize()
    {
    }

    public ConfigurationModel Configuration { get; set; }
}