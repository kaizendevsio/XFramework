using Microsoft.EntityFrameworkCore;
using XFramework.Domain.Contexts;
using IdentityServer.Domain.Shared.Contracts;
using Wallets.Domain.Shared.Contracts;
using Wallets.Domain.Shared.Enums;
using XFramework.TestInfrastructure;

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

    protected AppDbContext CreateDbContext(Guid? tenantId = null)
    {
        var effectiveTenantId = tenantId ?? WalletsTestFixture.TestTenantId;
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(WalletsTestFixture.ConnectionString)
            .Options;

        var config = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Tenant:DefaultId"] = effectiveTenantId.ToString()
            })
            .Build();

        return new AppDbContext(
            options,
            new Microsoft.AspNetCore.Http.HttpContextAccessor(),
            config,
            new TestEffectiveTenantContextAccessor(effectiveTenantId));
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

    protected async Task<Guid> SeedApprovedWalletApproval(
        Guid walletId,
        WalletOperationType operationType,
        Guid? requesterCredentialId = null,
        Guid? approverCredentialId = null)
    {
        await using var db = CreateDbContext();

        var approval = new WalletApprovalRequest
        {
            Id = Guid.NewGuid(),
            TenantId = WalletsTestFixture.TestTenantId,
            OperationType = operationType,
            WalletId = walletId,
            Status = WalletApprovalStatus.Approved,
            RequesterCredentialId = requesterCredentialId ?? Guid.NewGuid(),
            ApproverCredentialId = approverCredentialId ?? Guid.NewGuid(),
            RequestedAt = DateTime.UtcNow.AddMinutes(-5),
            DecidedAt = DateTime.UtcNow.AddMinutes(-1),
            DecisionReason = "approved by integration test"
        };

        db.Set<WalletApprovalRequest>().Add(approval);
        await db.SaveChangesAsync();
        return approval.Id;
    }

    protected static XFramework.Domain.Shared.BusinessObjects.RequestMetadata CreateMetadata() => new()
    {
        RequestedTenantId = WalletsTestFixture.TestTenantId,
        RequestId = Guid.NewGuid(),
        IpAddress = "127.0.0.1",
        OperationName = "WalletTest",
        DeviceName = "TestDevice",
        UserAgent = "TestAgent"
    };
}
