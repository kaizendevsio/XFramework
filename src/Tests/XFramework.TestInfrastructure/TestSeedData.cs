using Microsoft.EntityFrameworkCore;
using XFramework.Domain.Contexts;
using Contracts = XFramework.Domain.Shared.Contracts;

namespace XFramework.TestInfrastructure;

/// <summary>
/// Seeds shared reference data needed by all modules (tenant, role types, registry config, wallet types, etc).
/// Called once per test run after database migration.
/// </summary>
public static class TestSeedData
{
    public static async Task SeedAll(AppDbContext db)
    {
        await SeedTenant(db);
        await SeedIdentityRoles(db);
        await SeedRegistryConfig(db);
        await SeedVerificationTypes(db);
        await SeedSessionTypes(db);
        await SeedWalletTypes(db);
        await db.SaveChangesAsync();
    }

    private static async Task SeedTenant(AppDbContext db)
    {
        if (!await db.Set<Contracts.Tenant>().AnyAsync(t => t.Id == TestConstants.TenantId))
        {
            db.Set<Contracts.Tenant>().Add(new Contracts.Tenant
            {
                Id = TestConstants.TenantId,
                TenantId = TestConstants.TenantId,
                Name = "Test Tenant",
                Description = "Shared integration test tenant"
            });
        }
    }

    private static async Task SeedIdentityRoles(AppDbContext db)
    {
        if (!await db.Set<Contracts.IdentityRoleTypeGroup>().AnyAsync(g => g.Id == TestConstants.RoleGroupId))
        {
            db.Set<Contracts.IdentityRoleTypeGroup>().Add(new Contracts.IdentityRoleTypeGroup
            {
                Id = TestConstants.RoleGroupId,
                Name = "Default",
                Description = "Default role group",
                TenantId = TestConstants.TenantId
            });
        }

        if (!await db.Set<Contracts.IdentityRoleType>().AnyAsync(r => r.Id == TestConstants.RoleTypeId))
        {
            db.Set<Contracts.IdentityRoleType>().Add(new Contracts.IdentityRoleType
            {
                Id = TestConstants.RoleTypeId,
                Name = "User",
                GroupId = TestConstants.RoleGroupId,
                TenantId = TestConstants.TenantId
            });
        }
    }

    private static async Task SeedRegistryConfig(AppDbContext db)
    {
        if (!await db.Set<Contracts.RegistryConfigurationGroup>().AnyAsync(g => g.Id == TestConstants.RegistryGroupId))
        {
            db.Set<Contracts.RegistryConfigurationGroup>().Add(new Contracts.RegistryConfigurationGroup
            {
                Id = TestConstants.RegistryGroupId,
                Name = "Config",
                Description = "Test configuration",
                TenantId = TestConstants.TenantId
            });
        }

        // Auth: DefaultAuthorizeBy
        await SeedRegistryEntry(db, "DefaultAuthorizeBy", "1");

        // Wallets: Transfer deduction type
        await SeedRegistryEntry(db, "Settings:Wallet:Transfer:DeductionType", "DeductFromSender");
    }

    private static async Task SeedRegistryEntry(AppDbContext db, string key, string value)
    {
        if (!await db.Set<Contracts.RegistryConfiguration>().AnyAsync(r => r.Key == key && r.TenantId == TestConstants.TenantId))
        {
            db.Set<Contracts.RegistryConfiguration>().Add(new Contracts.RegistryConfiguration
            {
                Id = Guid.NewGuid(),
                Key = key,
                Value = value,
                GroupId = TestConstants.RegistryGroupId,
                TenantId = TestConstants.TenantId
            });
        }
    }

    private static async Task SeedVerificationTypes(AppDbContext db)
    {
        if (!await db.Set<Contracts.IdentityVerificationType>().AnyAsync(v => v.Name == "Sms"))
        {
            db.Set<Contracts.IdentityVerificationType>().Add(new Contracts.IdentityVerificationType
            {
                Id = Guid.NewGuid(),
                Name = "Sms",
                TenantId = TestConstants.TenantId
            });
        }
    }

    private static async Task SeedSessionTypes(AppDbContext db)
    {
        var sessionTypes = new (string Name, Guid SystemReferenceId)[]
        {
            ("User", Guid.Parse("70b44b35-bf8e-43fc-af1a-38bdb816d51f")),
            ("Service", Guid.Parse("1e3ab070-386a-410d-823f-4f225e07a69c")),
            ("Token", Guid.Parse("d71cda39-4192-4d7b-af22-1c6c9289b913"))
        };

        foreach (var (name, sysRefId) in sessionTypes)
        {
            if (!await db.Set<Contracts.SessionType>().AnyAsync(s => s.Name == name && s.TenantId == TestConstants.TenantId))
            {
                db.Set<Contracts.SessionType>().Add(new Contracts.SessionType
                {
                    Id = Guid.NewGuid(),
                    Name = name,
                    SystemReferenceId = sysRefId,
                    TenantId = TestConstants.TenantId
                });
            }
        }
    }

    private static async Task SeedWalletTypes(AppDbContext db)
    {
        if (!await db.Set<Contracts.WalletType>().AnyAsync(w => w.Id == TestConstants.WalletTypeId))
        {
            db.Set<Contracts.WalletType>().Add(new Contracts.WalletType
            {
                Id = TestConstants.WalletTypeId,
                Code = "TST",
                Name = "TestCoin",
                TenantId = TestConstants.TenantId,
                MinTransferRule = 0,
                MaxTransferRule = 1_000_000
            });
        }

        if (!await db.Set<Contracts.WalletType>().AnyAsync(w => w.Id == TestConstants.WalletType2Id))
        {
            db.Set<Contracts.WalletType>().Add(new Contracts.WalletType
            {
                Id = TestConstants.WalletType2Id,
                Code = "TST2",
                Name = "TestCoin2",
                TenantId = TestConstants.TenantId,
                MinTransferRule = 0,
                MaxTransferRule = 1_000_000
            });
        }
    }
}
