using System.Reflection;

namespace POS.IntegrationTests;

[SetUpFixture]
public sealed class PosTestAssemblySetup
{
    [OneTimeSetUp]
    public void LoadReferenceDataMappingsBeforeFirstContext()
    {
        // AppDbContext discovers loaded assemblies once, then EF caches the model across fixtures.
        Assembly.Load("IdentityServer.Domain.Shared");
        Assembly.Load("Wallets.Domain.Shared");
        Assembly.Load("POS.Domain.Shared");
    }
}
