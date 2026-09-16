using IdentityServer.Api.Services;
using IdentityServer.Domain.Shared.Configurations;
using IdentityServer.Domain.Shared.Contracts;
using IdentityServer.Domain.Shared.Contracts.Requests;
using IdentityServer.Domain.Shared.Contracts.Responses;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Moq;
using XFramework.Core.Patterns;
using NUnit.Framework;
using Testcontainers.PostgreSql;
using XFramework.Integration.Security;

// Independent focused PostgreSQL fixture; does not start the module-wide host stack.
namespace IdentityServer.EncryptionTests;

[TestFixture]
public sealed class EncryptionDirectoryPersistenceTests
{
    private PostgreSqlContainer _postgres = null!;
    private Guid _tenant;
    private Guid _owner;

    [OneTimeSetUp]
    public async Task Start()
    {
        _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
        await _postgres.StartAsync();
        await using var db = Context();
        await db.Database.EnsureCreatedAsync();
    }

    [OneTimeTearDown] public async Task Stop() => await _postgres.DisposeAsync();
    [SetUp] public void Identity() { _tenant = Guid.NewGuid(); _owner = Guid.NewGuid(); }

    [Test]
    public async Task Migration_AddsOnlyOwnedTableAndMessageColumns_WithSafeExistingMessageDefaults()
    {
        var database = "migration_" + Guid.NewGuid().ToString("N");
        await using (var admin = new NpgsqlConnection(_postgres.GetConnectionString()))
        {
            await admin.OpenAsync();
            await using var create = new NpgsqlCommand($"CREATE DATABASE {database}", admin);
            await create.ExecuteNonQueryAsync();
        }
        var connection = new NpgsqlConnectionStringBuilder(_postgres.GetConnectionString()) { Database = database }.ConnectionString;
        await using var db = new Store(new DbContextOptionsBuilder<Store>().UseNpgsql(connection).Options);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE SCHEMA "Identity";
            CREATE SCHEMA "Communications";
            CREATE TABLE "Communications"."Message" ("Id" uuid PRIMARY KEY);
            CREATE TABLE "Communications"."MessageThread" ("Id" uuid PRIMARY KEY);
            INSERT INTO "Communications"."Message" VALUES ('00000000-0000-0000-0000-000000000001');
            INSERT INTO "Communications"."MessageThread" VALUES ('00000000-0000-0000-0000-000000000001');
            """);
        var migration = new XFramework.Domain.Migrations.AddEncryptionDirectoryAndMessageEnvelopes();
        var generator = db.GetService<IMigrationsSqlGenerator>();
        foreach (var command in generator.Generate(migration.UpOperations))
            await db.Database.ExecuteSqlRawAsync(command.CommandText);
        foreach (var command in generator.Generate(new XFramework.Domain.Migrations.AddEncryptionIdentityPublicHistory().UpOperations))
            await db.Database.ExecuteSqlRawAsync(command.CommandText);
        Assert.That(await db.Database.SqlQueryRaw<bool>("SELECT \"EncryptionRequired\" AS \"Value\" FROM \"Communications\".\"MessageThread\"").SingleAsync(), Is.False);
        Assert.That(await db.Database.SqlQueryRaw<bool>("SELECT (\"EncryptedEnvelope\" IS NULL) AS \"Value\" FROM \"Communications\".\"Message\"").SingleAsync(), Is.True);
        Assert.That(await db.Set<EncryptionAccount>().CountAsync(), Is.Zero);
    }

    [Test]
    public async Task Initialize_DerivesOwnerAndTenant_IgnoresRequestedTenant()
    {
        await using var db = Context();
        var request = Directory();
        request.Metadata = new() { RequestedTenantId = Guid.NewGuid() };
        var result = await Service(db).PutEncryptionDirectoryAsync(request);
        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Data!.TenantId, Is.EqualTo(_tenant));
        Assert.That(result.Data.CredentialId, Is.EqualTo(_owner));
        Assert.That(result.Data.Revision, Is.EqualTo(1));
    }

    [Test]
    public async Task Read_SameTenantDirectoryOnly_RecoveryRemainsOwnerOnly()
    {
        await Initialize();
        await using var db = Context();
        Assert.That((await Service(db).PutEncryptionRecoveryAsync(new() { Archive = Armor("MESSAGE", "ciphertext") })).IsSuccess, Is.True);
        await using var other = Context();
        var peer = Service(other, credential: Guid.NewGuid());
        Assert.That((await peer.GetEncryptionDirectoryAsync(new() { CredentialId = _owner })).IsSuccess, Is.True);
        Assert.That((await peer.GetEncryptionRecoveryAsync(new())).StatusCode, Is.EqualTo(404));
        Assert.That((await Service(other, tenant: Guid.NewGuid()).GetEncryptionDirectoryAsync(new() { CredentialId = _owner })).StatusCode, Is.EqualTo(404));
    }

    [Test]
    public async Task MissingOrCrossTenantActor_CannotReadOrWrite()
    {
        await using var db = Context();
        var absent = new EncryptionDirectoryService(db, new Accessor(null));
        Assert.That((await absent.PutEncryptionDirectoryAsync(Directory())).StatusCode, Is.EqualTo(401));
        var wrong = new EncryptionDirectoryService(db, new Accessor(Actor(_tenant, _owner) with { EffectiveTenantId = Guid.NewGuid() }));
        Assert.That((await wrong.GetEncryptionRecoveryAsync(new())).StatusCode, Is.EqualTo(401));
        Assert.That((await wrong.PutEncryptionRecoveryAsync(new() { Archive = Armor("MESSAGE", "cipher") })).StatusCode, Is.EqualTo(401));
    }

    [Test]
    public async Task ConcurrentInitialization_OneRootWins()
    {
        await using var first = Context(); await using var second = Context();
        var result = await Task.WhenAll(Service(first).PutEncryptionDirectoryAsync(Directory()), Service(second).PutEncryptionDirectoryAsync(Directory()));
        Assert.That(result.Count(x => x.IsSuccess), Is.EqualTo(1));
        Assert.That(result.Count(x => x.StatusCode == 409), Is.EqualTo(1));
        Assert.That(await first.Set<EncryptionAccount>().CountAsync(x => x.TenantId == _tenant), Is.EqualTo(1));
    }

    [Test]
    public async Task ConcurrentDirectoryWrites_RejectStaleTrackedSnapshot()
    {
        var initial = await Initialize();
        await using var first = Context(); await using var second = Context();
        await first.Set<EncryptionAccount>().AsTracking().SingleAsync(x => x.TenantId == _tenant);
        await second.Set<EncryptionAccount>().AsTracking().SingleAsync(x => x.TenantId == _tenant);
        initial.ExpectedRevision = 1;
        Assert.That((await Service(first).PutEncryptionDirectoryAsync(initial)).IsSuccess, Is.True);
        Assert.That((await Service(second).PutEncryptionDirectoryAsync(initial)).StatusCode, Is.EqualTo(409));
    }

    [Test]
    public async Task ConcurrentRecoveryWrites_RejectStaleTrackedSnapshot()
    {
        await Initialize();
        await using var first = Context(); await using var second = Context();
        await first.Set<EncryptionAccount>().AsTracking().SingleAsync(x => x.TenantId == _tenant);
        await second.Set<EncryptionAccount>().AsTracking().SingleAsync(x => x.TenantId == _tenant);
        Assert.That((await Service(first).PutEncryptionRecoveryAsync(new() { Archive = Armor("MESSAGE", "first") })).IsSuccess, Is.True);
        Assert.That((await Service(second).PutEncryptionRecoveryAsync(new() { Archive = Armor("MESSAGE", "second") })).StatusCode, Is.EqualTo(409));
        await using var verify = Context();
        var stored = await Service(verify).GetEncryptionRecoveryAsync(new());
        Assert.That(stored.Data!.Archive, Is.EqualTo(Armor("MESSAGE", "first")));
        Assert.That(stored.Data.Revision, Is.EqualTo(1));
    }

    [TestCase("root")][TestCase("key")][TestCase("approval")][TestCase("remove")]
    public async Task Directory_CannotReplaceTrustedMaterial(string mutation)
    {
        var initial = await Initialize(); initial.ExpectedRevision = 1;
        switch (mutation)
        {
            case "root": initial.RootPublicKey = Armor("PUBLIC KEY BLOCK", "newroot"); break;
            case "key": initial.Devices[0].SigningPublicKey = Armor("PUBLIC KEY BLOCK", "newkey"); break;
            case "approval": initial.Devices[0].Approval = Armor("MESSAGE", "newapproval"); break;
            case "remove": initial.Devices = [Device()]; break;
        }
        await using var db = Context();
        Assert.That((await Service(db).PutEncryptionDirectoryAsync(initial)).StatusCode, Is.EqualTo(409));
    }

    [Test]
    public async Task RevocationIsPermanent_AndPublicKeysRemainReadable()
    {
        var initial = await Initialize(); initial.ExpectedRevision = 1;
        initial.Devices[0].Revocation = Armor("MESSAGE", "revoked");
        await using (var db = Context()) Assert.That((await Service(db).PutEncryptionDirectoryAsync(initial)).IsSuccess, Is.True);
        initial.ExpectedRevision = 2; initial.Devices[0].Revocation = null;
        await using var next = Context();
        Assert.That((await Service(next).PutEncryptionDirectoryAsync(initial)).StatusCode, Is.EqualTo(409));
        var read = await Service(next).GetEncryptionDirectoryAsync(new() { CredentialId = _owner });
        Assert.That(read.Data!.Devices[0].Revocation, Is.Not.Null);
        Assert.That(read.Data.Devices[0].SigningPublicKey, Is.EqualTo(initial.Devices[0].SigningPublicKey));
    }

    [TestCase("private")][TestCase("duplicate")][TestCase("large")][TestCase("overflow")][TestCase("samekey")]
    public async Task InvalidOrUnboundedDirectory_IsRejectedBeforePersistence(string mutation)
    {
        var request = Directory();
        switch (mutation)
        {
            case "private": request.RootPublicKey = Armor("PRIVATE KEY BLOCK", "secret"); break;
            case "duplicate": request.Devices.Add(request.Devices[0]); break;
            case "large": request.Roster = Armor("MESSAGE", new string('a', 524288)); break;
            case "overflow": request.ExpectedRevision = long.MaxValue; break;
            case "samekey": request.Devices[0].EncryptionPublicKey = request.Devices[0].SigningPublicKey; break;
        }
        await using var db = Context();
        Assert.That((await Service(db).PutEncryptionDirectoryAsync(request)).StatusCode, Is.EqualTo(400));
        Assert.That(await db.Set<EncryptionAccount>().AnyAsync(x => x.TenantId == _tenant), Is.False);
    }

    [TestCase("plaintext")][TestCase("private")][TestCase("large")]
    public async Task Recovery_RejectsPlaintextAndOversizedArchive(string mutation)
    {
        await Initialize();
        var archive = mutation switch { "private" => Armor("PRIVATE KEY BLOCK", "secret"), "large" => Armor("MESSAGE", new string('a', 2097152)), _ => "recovery secret" };
        await using var db = Context();
        Assert.That((await Service(db).PutEncryptionRecoveryAsync(new() { Archive = archive })).StatusCode, Is.EqualTo(400));
    }

    [Test]
    public async Task Reset_RechecksOwnPassword_AtomicallyReplacesKeysAndBackup_PreservesPublicHistory()
    {
        var original = await Initialize();
        var password = new Mock<IAuthService>();
        password.Setup(x => x.VerifyPasswordAsync(It.Is<VerifyPasswordRequest>(r => r.CredentialId == _owner), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<bool>.Success(true));
        var next = Directory(); next.ExpectedRevision = 1; next.RootPublicKey = Armor("PUBLIC KEY BLOCK", "new root"); next.Roster = Armor("MESSAGE", "new roster");
        var request = new ResetEncryptionIdentityRequest { Password = "fixture-password", Directory = next, RecoveryArchive = Armor("MESSAGE", "new backup") };
        await using var db = Context(); var service = Service(db);
        var reset = await service.ResetEncryptionIdentityAsync(request, password.Object);
        Assert.That(reset.IsSuccess, Is.True);
        Assert.That(reset.Data!.Revision, Is.EqualTo(2));
        Assert.That(reset.Data.RootPublicKey, Is.EqualTo(next.RootPublicKey));
        Assert.That((await service.GetEncryptionRecoveryAsync(new())).Data!.Archive, Is.EqualTo(request.RecoveryArchive));
        Assert.That((await service.ResetEncryptionIdentityAsync(request, password.Object)).Data!.Revision, Is.EqualTo(2), "Lost response retry is idempotent");
        var peer = Service(db, credential: Guid.NewGuid());
        var history = await peer.GetEncryptionDirectoryAsync(new() { CredentialId = _owner, SenderDeviceId = original.Devices[0].DeviceId });
        Assert.That(history.Data!.RootPublicKey, Is.EqualTo(original.RootPublicKey));
        Assert.That((await peer.GetEncryptionRecoveryAsync(new())).StatusCode, Is.EqualTo(404));
        Assert.That((await Service(db, tenant: Guid.NewGuid()).GetEncryptionDirectoryAsync(new() { CredentialId = _owner, SenderDeviceId = original.Devices[0].DeviceId })).StatusCode, Is.EqualTo(404));
        Assert.That((await service.PutEncryptionRecoveryAsync(new() { ExpectedRevision = 1, Archive = Armor("MESSAGE", "old device backup") })).StatusCode, Is.EqualTo(409));
        original.ExpectedRevision = 2;
        Assert.That((await service.PutEncryptionDirectoryAsync(original)).StatusCode, Is.EqualTo(409));
        Assert.That((await service.ResetEncryptionIdentityAsync(request with { Directory = original }, password.Object)).StatusCode, Is.EqualTo(409));
        password.Verify(x => x.VerifyPasswordAsync(It.IsAny<VerifyPasswordRequest>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
    }

    [TestCase(403)][TestCase(429)]
    public async Task Reset_FailedPasswordOrRateLimit_LeavesDirectoryAndBackupUntouched(int status)
    {
        var original = await Initialize();
        var password = new Mock<IAuthService>();
        password.Setup(x => x.VerifyPasswordAsync(It.IsAny<VerifyPasswordRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<bool>.Failure("Denied", status));
        await using var db = Context(); var service = Service(db);
        var request = new ResetEncryptionIdentityRequest { Password = "wrong-password", Directory = Directory(), RecoveryArchive = Armor("MESSAGE", "backup") };
        Assert.That((await service.ResetEncryptionIdentityAsync(request, password.Object)).StatusCode, Is.EqualTo(status));
        Assert.That((await service.GetEncryptionDirectoryAsync(new() { CredentialId = _owner })).Data!.RootPublicKey, Is.EqualTo(original.RootPublicKey));
        Assert.That((await service.GetEncryptionRecoveryAsync(new())).Data!.Revision, Is.Zero);
        Assert.That((await service.ResetEncryptionIdentityAsync(request with { Password = null! }, password.Object)).StatusCode, Is.EqualTo(400));
    }

    [Test]
    public async Task Reset_OpaqueProof_RewrapsBackupAtomicallyAndCannotReplayOrUseLegacyPassword()
    {
        var original = await Initialize();
        var epoch = Guid.NewGuid();
        await using var db = Context();
        db.Add(new OpaqueCredential { TenantId = _tenant, CredentialId = _owner, Epoch = epoch, Record = "test record", WrappedRecovery = "old envelope", UpdatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var grants = new IdentityServer.Api.Infrastructure.OpaqueExchanges(TimeProvider.System);
        var service = new EncryptionDirectoryService(db, new Accessor(Actor(_tenant, _owner)), grants);
        var verifier = new Mock<IAuthService>(MockBehavior.Strict);
        var next = Directory() with { ExpectedRevision = 1, RootPublicKey = Armor("PUBLIC KEY BLOCK", "new root") };
        var request = new ResetEncryptionIdentityRequest { Directory = next, RecoveryArchive = Armor("MESSAGE", "new encrypted backup"), Password = "old path must not work" };
        Assert.That((await service.ResetEncryptionIdentityAsync(request, verifier.Object)).StatusCode, Is.EqualTo(403));
        var grant = grants.Add(new(_tenant, Guid.NewGuid(), "user", "scope", _owner, epoch, Guid.Empty, "reauth", "", default));
        request.Password = ""; request.OpaqueProof = grant;
        request.WrappedRecovery = System.Text.Json.JsonSerializer.Serialize(new { v = 1, iv = Convert.ToBase64String(new byte[12]), ciphertext = Convert.ToBase64String(new byte[80]) });
        Assert.That((await service.ResetEncryptionIdentityAsync(request, verifier.Object)).IsSuccess, Is.True);
        db.ChangeTracker.Clear();
        var credential = await db.Set<OpaqueCredential>().SingleAsync(x => x.CredentialId == _owner && x.TenantId == _tenant);
        Assert.That(credential.WrappedRecovery, Is.EqualTo(request.WrappedRecovery));
        Assert.That(credential.Epoch, Is.Not.EqualTo(epoch));
        Assert.That((await service.GetEncryptionRecoveryAsync(new())).Data!.Archive, Is.EqualTo(request.RecoveryArchive));
        Assert.That((await service.ResetEncryptionIdentityAsync(request, verifier.Object)).StatusCode, Is.EqualTo(403));
        verifier.VerifyNoOtherCalls();
    }

    private async Task<PutEncryptionDirectoryRequest> Initialize()
    {
        await using var db = Context(); var request = Directory();
        Assert.That((await Service(db).PutEncryptionDirectoryAsync(request)).IsSuccess, Is.True);
        return request;
    }
    private static string Armor(string type, string payload) => $"-----BEGIN PGP {type}-----\n{payload}\n-----END PGP {type}-----";
    private static EncryptionDevice Device() => new()
    { DeviceId = Guid.NewGuid(), SigningPublicKey = Armor("PUBLIC KEY BLOCK", "signing"), EncryptionPublicKey = Armor("PUBLIC KEY BLOCK", "encryption"), Approval = Armor("MESSAGE", "approval") };
    private static PutEncryptionDirectoryRequest Directory() => new()
    { RootPublicKey = Armor("PUBLIC KEY BLOCK", "root"), Roster = Armor("MESSAGE", "roster"), Devices = [Device()] };
    private EncryptionDirectoryService Service(DbContext db, Guid? tenant = null, Guid? credential = null) => new(db, new Accessor(Actor(tenant ?? _tenant, credential ?? _owner)));
    private static TrustedInvocationContext Actor(Guid tenant, Guid credential) => new(new TrustedActorIdentity(credential, null, tenant, Guid.NewGuid(), new HashSet<string>(), new HashSet<string>(), "test", DateTimeOffset.UtcNow.AddHours(1)), null, tenant, tenant, Guid.NewGuid());
    private sealed class Accessor(TrustedInvocationContext? value) : ITrustedInvocationContextAccessor { public TrustedInvocationContext? Current => value; }
    private Store Context() => new(new DbContextOptionsBuilder<Store>().UseNpgsql(_postgres.GetConnectionString()).Options);
    private sealed class Store(DbContextOptions<Store> options) : DbContext(options)
    { protected override void OnModelCreating(ModelBuilder model) { model.ApplyConfiguration(new EncryptionAccountConfiguration()); model.ApplyConfiguration(new OpaqueCredentialConfiguration()); } }
}
