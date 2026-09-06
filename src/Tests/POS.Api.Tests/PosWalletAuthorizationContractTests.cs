namespace POS.Api.Tests;

public sealed class PosWalletAuthorizationContractTests
{
    [TestCase("AddFunds")]
    [TestCase("WithdrawFunds")]
    [TestCase("Transfer")]
    public void PosWalletOperations_AllowAuthorizedDelegatedTenantActors(string feature)
    {
        var repositoryRoot = FindRepositoryRoot();
        var endpoint = File.ReadAllText(Path.Combine(
            repositoryRoot.FullName,
            "src",
            "Modules",
            "XFramework.Wallets",
            "Wallets.Api",
            "Features",
            "Wallets",
            feature,
            "Endpoint.cs"));

        endpoint.Should().Contain("TenantAccessMode = TenantAccessMode.DelegatedTenant");
        endpoint.Should().Contain("RequiredActorCapabilities = [WalletAuthorizationCapabilities.Update]");
        endpoint.Should().Contain(
            "RequiredCrossTenantActorCapabilities = [XFrameworkActorCapabilities.IdentityTenantsManage]");
    }

    private static DirectoryInfo FindRepositoryRoot()
    {
        var current = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (current is not null && !Directory.Exists(Path.Combine(current.FullName, "src")))
            current = current.Parent;

        return current ?? throw new DirectoryNotFoundException("Could not locate repository root.");
    }
}
