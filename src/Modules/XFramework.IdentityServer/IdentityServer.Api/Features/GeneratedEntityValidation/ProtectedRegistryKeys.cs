namespace IdentityServer.Api.Features.GeneratedEntityValidation;

public static class ProtectedRegistryKeys
{
    public const string MutationError = "Finance settings must be changed through the dedicated Finance settings endpoint.";

    public static bool IsProtected(string? key) =>
        string.Equals(key?.Trim(), TenantFinanceSettings.RegistryKey, StringComparison.OrdinalIgnoreCase);
}
