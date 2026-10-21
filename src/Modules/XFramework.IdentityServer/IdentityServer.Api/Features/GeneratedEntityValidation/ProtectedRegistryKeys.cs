namespace IdentityServer.Api.Features.GeneratedEntityValidation;

public static class ProtectedRegistryKeys
{
    public const string MutationError = "Protected tenant settings must be changed through their dedicated settings endpoint.";

    public static bool IsProtected(string? key) =>
        string.Equals(key?.Trim(), TenantFinanceSettings.RegistryKey, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(key?.Trim(), POS.Domain.Shared.Contracts.PosCheckoutSettings.RegistryKey, StringComparison.OrdinalIgnoreCase);
}
