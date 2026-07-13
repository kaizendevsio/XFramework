namespace XFramework.Bolt.Phase0Synthetics;

public sealed class SyntheticConfigurationException(string code) : Exception
{
    public string Code { get; } = code;
}
