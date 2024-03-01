namespace XFramework.Integration.Extensions;

public static class XFrameworkExtensions
{
    public static void LoadMapsterDefaults()
    {
        TypeAdapterConfig.GlobalSettings.Default
            .PreserveReference(true)
            .IgnoreNullValues(true);
    }
}