using System.Text;
using IdentityServer.Domain.Shared.Contracts;
using Microsoft.EntityFrameworkCore;
using Wallets.Domain.Shared.Contracts;
using XFramework.Domain.Contexts;
using XFramework.Domain.Shared.BusinessObjects;

namespace XFramework.TestInfrastructure;

/// <summary>
/// Shared helper methods for seeding test entities and creating reusable objects.
/// </summary>
public static class TestHelpers
{
    public static string UniqueUsername() => $"testuser_{Guid.NewGuid():N}";
    public static string UniqueEmail() => $"test_{Guid.NewGuid():N}@test.com";
    public static string UniquePhone() => $"+1{Random.Shared.Next(1000000000, 1999999999)}";

    public static AppDbContext CreateDbContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Tenant:DefaultId"] = TestConstants.TenantId.ToString()
            })
            .Build();

        return new AppDbContext(
            options,
            new Microsoft.AspNetCore.Http.HttpContextAccessor(),
            config,
            new TestEffectiveTenantContextAccessor(TestConstants.TenantId));
    }

    public static RequestMetadata CreateMetadata() => new()
    {
        RequestedTenantId = TestConstants.TenantId,
        RequestId = Guid.NewGuid(),
        IpAddress = "127.0.0.1",
        OperationName = "IntegrationTest",
        DeviceName = "TestDevice",
        UserAgent = "TestAgent"
    };

    /// <summary>
    /// Seeds an IdentityInformation + IdentityCredential with BCrypt-hashed password.
    /// </summary>
    public static async Task<IdentityCredential> SeedCredentialWithRole(
        string connectionString,
        string? username = null,
        string password = "TestPassword123!")
    {
        await using var db = CreateDbContext(connectionString);

        var info = new IdentityInformation
        {
            Id = Guid.NewGuid(),
            FirstName = "Test",
            LastName = "User",
            TenantId = TestConstants.TenantId,
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow,
            ConcurrencyStamp = Guid.NewGuid()
        };
        db.Set<IdentityInformation>().Add(info);

        var credential = new IdentityCredential
        {
            Id = Guid.NewGuid(),
            UserName = username ?? UniqueUsername(),
            PasswordByte = Encoding.ASCII.GetBytes(
                BCrypt.Net.BCrypt.HashPassword(password, workFactor: 11)),
            IdentityInfoId = info.Id,
            IsEnabled = true,
            TenantId = TestConstants.TenantId,
            CreatedAt = DateTime.UtcNow,
            ConcurrencyStamp = Guid.NewGuid()
        };
        db.Set<IdentityCredential>().Add(credential);

        var role = new IdentityRole
        {
            Id = Guid.NewGuid(),
            CredentialId = credential.Id,
            TypeId = TestConstants.RoleTypeId,
            RoleExpiration = DateTime.UtcNow.AddYears(1),
            TenantId = TestConstants.TenantId,
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow,
            ConcurrencyStamp = Guid.NewGuid()
        };
        db.Set<IdentityRole>().Add(role);

        await db.SaveChangesAsync();
        return credential;
    }

    /// <summary>
    /// Seeds a credential without password (for wallet tests that don't need auth).
    /// </summary>
    public static async Task<IdentityCredential> SeedCredential(string connectionString)
    {
        await using var db = CreateDbContext(connectionString);

        var info = new IdentityInformation
        {
            Id = Guid.NewGuid(),
            FirstName = "Test",
            LastName = "User",
            TenantId = TestConstants.TenantId
        };
        db.Set<IdentityInformation>().Add(info);

        var credential = new IdentityCredential
        {
            Id = Guid.NewGuid(),
            UserName = UniqueUsername(),
            IdentityInfoId = info.Id,
            IsEnabled = true,
            TenantId = TestConstants.TenantId
        };
        db.Set<IdentityCredential>().Add(credential);

        await db.SaveChangesAsync();
        return credential;
    }

    /// <summary>
    /// Seeds a wallet for a given credential.
    /// </summary>
    public static async Task<Wallet> SeedWallet(
        string connectionString,
        Guid credentialId,
        decimal balance = 1000m,
        Guid? walletTypeId = null)
    {
        await using var db = CreateDbContext(connectionString);

        var wallet = new Wallet
        {
            Id = Guid.NewGuid(),
            CredentialId = credentialId,
            WalletTypeId = walletTypeId ?? TestConstants.WalletTypeId,
            Balance = balance,
            TransferableBalance = balance,
            TenantId = TestConstants.TenantId
        };
        db.Set<Wallet>().Add(wallet);

        await db.SaveChangesAsync();
        return wallet;
    }

    /// <summary>
    /// Seeds a credential with a phone contact (for verification tests).
    /// </summary>
    public static async Task<IdentityCredential> SeedCredentialWithContact(string connectionString)
    {
        await using var db = CreateDbContext(connectionString);

        var info = new IdentityInformation
        {
            Id = Guid.NewGuid(),
            FirstName = "Test",
            LastName = "User",
            TenantId = TestConstants.TenantId
        };
        db.Set<IdentityInformation>().Add(info);

        var credential = new IdentityCredential
        {
            Id = Guid.NewGuid(),
            UserName = UniqueUsername(),
            PasswordByte = Encoding.ASCII.GetBytes(
                BCrypt.Net.BCrypt.HashPassword("TestPassword123!", workFactor: 11)),
            IdentityInfoId = info.Id,
            IsEnabled = true,
            TenantId = TestConstants.TenantId
        };
        db.Set<IdentityCredential>().Add(credential);

        // Ensure ContactGroup exists
        if (!await db.Set<IdentityContactGroup>().AnyAsync(g => g.Id == TestConstants.ContactGroupId))
        {
            db.Set<IdentityContactGroup>().Add(new IdentityContactGroup
            {
                Id = TestConstants.ContactGroupId,
                Name = "Default",
                TenantId = TestConstants.TenantId
            });
        }

        var contactType = await db.Set<IdentityContactType>()
            .FirstOrDefaultAsync(c => c.Name == "Phone");

        if (contactType == null)
        {
            contactType = new IdentityContactType
            {
                Id = Guid.NewGuid(),
                Name = "Phone",
                TenantId = TestConstants.TenantId
            };
            db.Set<IdentityContactType>().Add(contactType);
        }

        db.Set<IdentityContact>().Add(new IdentityContact
        {
            Id = Guid.NewGuid(),
            Value = UniquePhone(),
            TypeId = contactType.Id,
            GroupId = TestConstants.ContactGroupId,
            CredentialId = credential.Id,
            TenantId = TestConstants.TenantId
        });

        await db.SaveChangesAsync();
        return credential;
    }
}
