namespace XFramework.Domain.Shared.Contracts;

public static class StorageAuthorizationCapabilities
{
    public const string Feature = "storage";
    public const string ViewKey = "view";
    public const string ManageKey = "manage";
    public const string View = $"{Feature}:{ViewKey}";
    public const string Manage = $"{Feature}:{ManageKey}";

    public static IReadOnlyList<string> All { get; } = [View, Manage];
}
