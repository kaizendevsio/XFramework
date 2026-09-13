using System.Data.Common;
using Communications.Api.Services;
using IdentityServer.Domain.Shared.Configurations;
using IdentityServer.Domain.Shared.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NUnit.Framework;
using Testcontainers.PostgreSql;

namespace Communications.Tests.Services;

[TestFixture, Category("Kind:Integration")]
public sealed class MessageEncryptionDirectoryReaderTests
{
    [Test]
    public async Task PublicProjection_IsTenantScoped_ExcludesInactiveCredentials_AndNeverSelectsRecovery()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
        await postgres.StartAsync();
        var commands = new CapturedCommands();
        var options = new DbContextOptionsBuilder<DirectoryDb>().UseNpgsql(postgres.GetConnectionString()).AddInterceptors(commands).Options;
        await using var db = new DirectoryDb(options);
        await db.Database.EnsureCreatedAsync();
        var tenant = Guid.NewGuid(); var foreign = Guid.NewGuid();
        var active = Guid.NewGuid(); var disabled = Guid.NewGuid(); var deleted = Guid.NewGuid(); var missing = Guid.NewGuid(); var mismatched = Guid.NewGuid();
        db.AddRange(
            Account(tenant, active, 7), Account(foreign, active, 99),
            Account(tenant, disabled, 2), Account(tenant, deleted, 3),
            Account(tenant, missing, 4), Account(tenant, mismatched, 5),
            new IdentityCredential { Id = active, TenantId = tenant, IsEnabled = true },
            new IdentityCredential { Id = disabled, TenantId = tenant, IsEnabled = false },
            new IdentityCredential { Id = deleted, TenantId = tenant, IsEnabled = true, IsDeleted = true },
            new IdentityCredential { Id = mismatched, TenantId = foreign, IsEnabled = true });
        await db.SaveChangesAsync(); db.ChangeTracker.Clear(); commands.Sql.Clear();
        var reader = new MessageEncryptionDirectoryReader(db);
        var result = await reader.ReadAsync(tenant, [active, disabled, deleted, missing, mismatched], CancellationToken.None);
        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].CredentialId, Is.EqualTo(active));
        Assert.That(result[0].Revision, Is.EqualTo(7));
        Assert.That(result[0].DevicesJson, Is.EqualTo("[]"));
        Assert.That(db.ChangeTracker.Entries(), Is.Empty);
        Assert.That(commands.Sql, Has.Count.EqualTo(1));
        Assert.That(commands.Sql[0], Does.Contain("\"DevicesJson\""));
        Assert.That(commands.Sql[0], Does.Not.Contain("\"RecoveryArchive\""));
        Assert.That(commands.Sql[0], Does.Not.Contain("\"RecoveryRevision\""));
        Assert.That(commands.Sql[0], Does.Not.Contain("\"PasswordByte\""));
        Assert.That(commands.Sql[0], Does.Not.Contain("\"RootPublicKey\""));
        Assert.That(await reader.ReadAsync(Guid.NewGuid(), [active], CancellationToken.None), Is.Empty);
    }

    private static EncryptionAccount Account(Guid tenant, Guid credential, long revision) => new()
    { TenantId = tenant, CredentialId = credential, DirectoryRevision = revision, DevicesJson = "[]",
        RootPublicKey = "public root excluded from send-validation projection", RecoveryArchive = "opaque recovery must not be selected" };

    private sealed class CapturedCommands : DbCommandInterceptor
    {
        public List<string> Sql { get; } = [];
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        { Sql.Add(command.CommandText); return ValueTask.FromResult(result); }
    }

    private sealed class DirectoryDb(DbContextOptions<DirectoryDb> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder model)
        {
            model.ApplyConfiguration(new EncryptionAccountConfiguration());
            // The reader needs only these credential columns; Identity owns its full relational model.
            var credential = model.Entity<IdentityCredential>();
            HashSet<string> required = [nameof(IdentityCredential.Id), nameof(IdentityCredential.TenantId),
                nameof(IdentityCredential.IsEnabled), nameof(IdentityCredential.IsDeleted)];
            foreach (var property in typeof(IdentityCredential).GetProperties().Where(x => !required.Contains(x.Name)))
                credential.Ignore(property.Name);
            credential.ToTable("IdentityCredential", "Identity"); credential.HasKey(x => x.Id);
        }
    }
}
