namespace IdentityServer.Domain.Shared.Contracts;

public static class IdentityAuthorizationConstants
{
    public const string View = "view";
    public const string Create = "create";
    public const string Update = "update";
    public const string Delete = "delete";
    public const string Manage = "manage";

    public const string Identity = "identity";
    public const string IdentityUsers = "identity.users";
    public const string IdentityCredentials = "identity.credentials";
    public const string IdentityRoles = "identity.roles";
    public const string IdentityTenants = "identity.tenants";
    public const string IdentitySessions = "identity.sessions";
    public const string IdentityVerifications = "identity.verifications";
    public const string IdentityContacts = "identity.contacts";
    public const string IdentityAddresses = "identity.addresses";
    public const string IdentityAuthLogs = "identity.auth_logs";

    public static IReadOnlyList<string> CapabilityKeys { get; } =
        [View, Create, Update, Delete, Manage];
}
