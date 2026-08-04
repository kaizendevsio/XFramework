using FluentAssertions;
using XFramework.TestInfrastructure;

namespace IdentityServer.IntegrationTests.Tests;

[TestFixture]
[Category(TestCategories.Integration)]
[Category(TestCategories.IdentityServer)]
public sealed class BackgroundDispatcherAuthorizationContractTests
{
    private static readonly string ServicesDirectory = Path.GetFullPath(Path.Combine(
        TestContext.CurrentContext.TestDirectory,
        "..", "..", "..", "..", "..",
        "Modules", "XFramework.IdentityServer", "IdentityServer.Api", "Services"));

    [TestCase("PasswordResetOutboxDispatcher.cs")]
    [TestCase("VerificationDeliveryOutboxDispatcher.cs")]
    [TestCase("StorageClaimOutboxDispatcher.cs")]
    [TestCase("StorageCleanupOutboxDispatcher.cs")]
    public void Dispatcher_AuthorizesTargetTenantBeforeMutatingOutbox(string fileName)
    {
        var source = File.ReadAllText(Path.Combine(ServicesDirectory, fileName));
        var authorizationIndex = source.IndexOf(
            "contextInitializer.EstablishAsync(",
            StringComparison.Ordinal);
        var failureReturnIndex = source.IndexOf(
            "if (!authorization.IsSuccess)",
            authorizationIndex,
            StringComparison.Ordinal);
        var mutationIndex = source.IndexOf(
            ".ExecuteUpdateAsync(",
            failureReturnIndex,
            StringComparison.Ordinal);

        authorizationIndex.Should().BeGreaterThanOrEqualTo(0);
        failureReturnIndex.Should().BeGreaterThan(authorizationIndex);
        mutationIndex.Should().BeGreaterThan(failureReturnIndex);

        var failureBranch = source[failureReturnIndex..mutationIndex];
        failureBranch.Should().Contain("return;");
        failureBranch.Should().NotContain("SaveChangesAsync(");
        failureBranch.Should().NotContain("ExecuteUpdateAsync(");
    }
}
