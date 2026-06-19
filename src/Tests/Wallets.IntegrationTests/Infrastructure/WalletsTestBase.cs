using Microsoft.EntityFrameworkCore;
using XFramework.Domain.Contexts;
using IdentityServer.Domain.Shared.Contracts;
using Wallets.Domain.Shared.Contracts;
using Wallets.Domain.Shared.Enums;

namespace Wallets.IntegrationTests;

public abstract class WalletsTestBase
{
    protected HttpClient HttpClient { get; private set; } = null!;

    [SetUp]
    public void BaseSetUp()
    {
        HttpClient = new HttpClient { BaseAddress = new Uri(WalletsTestFixture.WalletsUrl) };
    }

    [TearDown]
    public void BaseTearDown() => HttpClient?.Dispose();

    protected AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(WalletsTestFixture.ConnectionString)
            .Options;

        var config = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Tenant:DefaultId"] = WalletsTestFixture.TestTenantId.ToString()
            })
            .Build();

        return new AppDbContext(options, new Microsoft.AspNetCore.Http.HttpContextAccessor(), config);
    }

    protected async Task<IdentityCredential> SeedCredential()
    {
        await using var db = CreateDbContext();

        var info = new IdentityInformation
        {
            Id = Guid.NewGuid(),
            FirstName = "Test",
            LastName = "User",
            TenantId = WalletsTestFixture.TestTenantId
        };
        db.Set<IdentityInformation>().Add(info);

        var credential = new IdentityCredential
        {
            Id = Guid.NewGuid(),
            UserName = $"wallet_test_{Guid.NewGuid():N}",
            IdentityInfoId = info.Id,
            IsEnabled = true,
            TenantId = WalletsTestFixture.TestTenantId
        };
        db.Set<IdentityCredential>().Add(credential);

        await db.SaveChangesAsync();
        return credential;
    }

    protected async Task<Wallet> SeedWallet(
        Guid credentialId,
        decimal balance = 1000m,
        WalletStatus status = WalletStatus.Active)
    {
        await using var db = CreateDbContext();

        var wallet = new Wallet
        {
            Id = Guid.NewGuid(),
            CredentialId = credentialId,
            WalletTypeId = WalletsTestFixture.TestWalletTypeId,
            Balance = balance,
            TransferableBalance = balance,
            Status = status,
            TenantId = WalletsTestFixture.TestTenantId
        };
        db.Set<Wallet>().Add(wallet);

        await db.SaveChangesAsync();
        return wallet;
    }

    protected static XFramework.Domain.Shared.BusinessObjects.RequestMetadata CreateMetadata() => new()
    {
        TenantId = WalletsTestFixture.TestTenantId,
        RequestId = Guid.NewGuid(),
        IpAddress = "127.0.0.1",
        Name = "WalletTest",
        DeviceName = "TestDevice",
        DeviceAgent = "TestAgent"
    };
}
