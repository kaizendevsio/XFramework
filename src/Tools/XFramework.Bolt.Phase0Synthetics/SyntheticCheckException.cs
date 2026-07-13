namespace XFramework.Bolt.Phase0Synthetics;

public sealed class SyntheticCheckException(string code) : Exception
{
    public string Code { get; } = code;
}
