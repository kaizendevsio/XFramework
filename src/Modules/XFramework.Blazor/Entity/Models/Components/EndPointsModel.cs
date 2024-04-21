namespace XFramework.Blazor.Entity.Models.Components;

public class EndPointsModel
{
    public EndPointsModel()
    {
        _version = 1.0m;
    }
    public EndPointsModel(decimal version)
    {
        _version = version;
    }

    private static decimal _version = 1.0m;
    private static string Version = $"V{_version}/";
    public const string BaseUrl = "https://";
}