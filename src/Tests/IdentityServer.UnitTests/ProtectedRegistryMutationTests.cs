using FluentAssertions;
using FluentValidation;
using IdentityServer.Api.Features.GeneratedEntityValidation;
using IdentityServer.Api.Installers;
using IdentityServer.Domain.Shared.Contracts;
using MemoryPack;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;
using XFramework.Core.DataContext;
using XFramework.Core.Patterns;
using XFramework.Core.Services.FeatureGates;
using XFramework.Domain.Shared.DataContext;
using XFramework.Domain.Shared.ServiceIdentity;
using XFramework.Integration.Security;
using ITenantModuleFeatureService = XFramework.Core.Services.FeatureGates.ITenantModuleFeatureService;

namespace IdentityServer.UnitTests;

[TestFixture]
public sealed class ProtectedRegistryMutationTests
{
    [TestCase("finance.settings.v1")]
    [TestCase("FINANCE.SETTINGS.V1")]
    [TestCase(" finance.settings.v1 ")]
    public async Task RequestValidators_ProtectedKey_RejectCreateAndUpdate(string key)
    {
        var groupId = Guid.NewGuid();
        var create = await new CreateRegistryConfigurationRequestValidator().ValidateAsync(new CreateRegistryConfigurationRequest
        { Key = key, GroupId = groupId });
        var update = await new UpdateRegistryConfigurationRequestValidator().ValidateAsync(new UpdateRegistryConfigurationRequest
        { Key = key, GroupId = groupId });
        create.Errors.Should().Contain(x => x.ErrorMessage == ProtectedRegistryKeys.MutationError);
        update.Errors.Should().Contain(x => x.ErrorMessage == ProtectedRegistryKeys.MutationError);
    }

    [Test]
    public void ServicesInstaller_RegistersProtectedGeneratedService()
    {
        var services = new ServiceCollection();
        new ServicesInstaller().InstallServices<object>(services,
            new ConfigurationBuilder().Build(), Mock.Of<IHostEnvironment>());
        services.Last(x => x.ServiceType == typeof(IRegistryConfigurationService)).ImplementationType
            .Should().Be(typeof(ProtectedRegistryConfigurationService));
    }

    [TestCase("create", false)]
    [TestCase("update", false)]
    [TestCase("rename-in", false)]
    [TestCase("rename-out", false)]
    [TestCase("delete", false)]
    [TestCase("create", true)]
    [TestCase("update", true)]
    [TestCase("rename-in", true)]
    [TestCase("rename-out", true)]
    [TestCase("delete", true)]
    public async Task GeneratedService_ProtectedMutation_IsRejectedWithoutPersistence(string operation, bool financeManager)
    {
        await using var fixture = await Fixture.CreateAsync(financeManager);
        var originalKey = operation == "rename-in" ? "ordinary" : TenantFinanceSettings.RegistryKey;
        var row = await fixture.SeedAsync(originalKey);
        (bool IsSuccess, int StatusCode, string? Message) result;
        if (operation == "delete")
        {
            var deleted = await fixture.Service.DeleteAsync(row.Id, row.ConcurrencyStamp);
            result = (deleted.IsSuccess, deleted.StatusCode, deleted.Message);
        }
        else
        {
            var changed = operation == "create"
                ? await fixture.Service.CreateAsync(new()
                { Key = TenantFinanceSettings.RegistryKey, GroupId = row.GroupId, Value = "replacement" })
                : await fixture.Service.UpdateAsync(row.Id, row.ConcurrencyStamp, new()
            {
                Key = operation == "rename-out" ? "ordinary" : TenantFinanceSettings.RegistryKey,
                GroupId = row.GroupId, Value = "replacement"
            });
            result = (changed.IsSuccess, changed.StatusCode, changed.Message);
        }

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(400);
        result.Message.Should().Contain(ProtectedRegistryKeys.MutationError);
        fixture.Db.ChangeTracker.Clear();
        var persisted = await fixture.Db.Set<RegistryConfiguration>().SingleAsync();
        persisted.Key.Should().Be(originalKey);
        persisted.Value.Should().Be("original");
        persisted.ConcurrencyStamp.Should().Be(row.ConcurrencyStamp);
    }

    [TestCase("create", false)]
    [TestCase("update", false)]
    [TestCase("rename-in", false)]
    [TestCase("rename-out", false)]
    [TestCase("delete", false)]
    [TestCase("create", true)]
    [TestCase("update", true)]
    [TestCase("rename-in", true)]
    [TestCase("rename-out", true)]
    [TestCase("delete", true)]
    public async Task RemoteDataContext_ProtectedMutation_IsRejectedWithoutPersistence(string operation, bool financeManager)
    {
        await using var fixture = await Fixture.CreateAsync(financeManager);
        var originalKey = operation == "rename-in" ? "ordinary" : TenantFinanceSettings.RegistryKey;
        var row = await fixture.SeedAsync(originalKey);
        ChangeEntry change;
        if (operation == "create")
        {
            change = FullChange(ChangeOperation.Add, new()
            {
                Id = Guid.NewGuid(), TenantId = fixture.TenantId, GroupId = row.GroupId,
                Key = TenantFinanceSettings.RegistryKey, Value = "replacement"
            });
        }
        else if (operation == "delete")
        {
            // A forged ordinary key must not hide the persisted protected key on removal.
            change = FullChange(ChangeOperation.Remove, new()
            { Id = row.Id, TenantId = fixture.TenantId, Key = "ordinary", ConcurrencyStamp = row.ConcurrencyStamp });
        }
        else
        {
            change = Patch(row, operation == "rename-out" ? "ordinary" : TenantFinanceSettings.RegistryKey);
        }

        var result = await fixture.ExecuteAsync(change);
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(400);
        result.Message.Should().Contain(ProtectedRegistryKeys.MutationError);
        fixture.Db.ChangeTracker.Clear();
        var persisted = await fixture.Db.Set<RegistryConfiguration>().SingleAsync();
        persisted.Key.Should().Be(originalKey);
        persisted.Value.Should().Be("original");
        persisted.ConcurrencyStamp.Should().Be(row.ConcurrencyStamp);
    }

    [Test]
    public async Task GeneratedService_OrdinaryCrud_RemainsAvailable()
    {
        await using var fixture = await Fixture.CreateAsync();
        var created = await fixture.Service.CreateAsync(new() { Key = "ordinary", GroupId = fixture.GroupId, Value = "one" });
        created.IsSuccess.Should().BeTrue(created.Message);
        var updated = await fixture.Service.UpdateAsync(created.Data!.Id, created.Data.ConcurrencyStamp,
            new() { Key = "renamed", GroupId = fixture.GroupId, Value = "two" });
        updated.IsSuccess.Should().BeTrue(updated.Message);
        var deleted = await fixture.Service.DeleteAsync(updated.Data!.Id, updated.Data.ConcurrencyStamp);
        deleted.IsSuccess.Should().BeTrue(deleted.Message);
        (await fixture.Db.Set<RegistryConfiguration>().CountAsync()).Should().Be(0);
    }

    [Test]
    public async Task RemoteDataContext_OrdinaryCrud_RemainsAvailable()
    {
        await using var fixture = await Fixture.CreateAsync();
        var row = new RegistryConfiguration
        { Id = Guid.NewGuid(), TenantId = fixture.TenantId, GroupId = fixture.GroupId, Key = "ordinary", Value = "one" };
        var created = await fixture.ExecuteAsync(FullChange(ChangeOperation.Add, row));
        created.IsSuccess.Should().BeTrue(created.Message);
        fixture.Db.ChangeTracker.Clear();
        row = await fixture.Db.Set<RegistryConfiguration>().SingleAsync();
        var updated = await fixture.ExecuteAsync(Patch(row, "renamed"));
        updated.IsSuccess.Should().BeTrue(updated.Message);
        fixture.Db.ChangeTracker.Clear();
        row = await fixture.Db.Set<RegistryConfiguration>().SingleAsync();
        row.Key.Should().Be("renamed");
        var deleted = await fixture.ExecuteAsync(FullChange(ChangeOperation.Remove, row));
        deleted.IsSuccess.Should().BeTrue(deleted.Message);
        (await fixture.Db.Set<RegistryConfiguration>().CountAsync()).Should().Be(0);
    }

    [Test]
    public async Task RemoteDataContext_GroupWithProtectedChild_IsRejectedWithoutPersistence()
    {
        await using var fixture = await Fixture.CreateAsync(financeManager: true);
        var group = new RegistryConfigurationGroup
        {
            Id = Guid.NewGuid(), TenantId = fixture.TenantId, Name = "Nested", SystemReferenceId = Guid.NewGuid(),
            RegistryConfigurations = [new()
            {
                Id = Guid.NewGuid(), TenantId = fixture.TenantId, Key = TenantFinanceSettings.RegistryKey,
                Value = "replacement", IsEnabled = true
            }]
        };
        var result = await fixture.ExecuteAsync(new()
        {
            EntityTypeName = nameof(RegistryConfigurationGroup), Operation = ChangeOperation.Add,
            SerializedEntity = MemoryPackSerializer.Serialize(group)
        });
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(403);
        result.Message.Should().Contain("navigation writes are not supported");
        fixture.Db.ChangeTracker.Clear();
        (await fixture.Db.Set<RegistryConfiguration>().CountAsync()).Should().Be(0);
        (await fixture.Db.Set<RegistryConfigurationGroup>().CountAsync()).Should().Be(1);
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task GeneratedDelete_UnauthorizedActor_IsRejectedBeforeProtectedKeyLookup(bool missingCapability)
    {
        await using var fixture = await Fixture.CreateAsync();
        var row = await fixture.SeedAsync(TenantFinanceSettings.RegistryKey);
        fixture.Invocation.Current = missingCapability
            ? fixture.Invocation.Current! with
            {
                Actor = new TrustedActorIdentity(Guid.NewGuid(), null, fixture.TenantId, Guid.NewGuid(),
                    new HashSet<string>(), new HashSet<string>(), "test", DateTimeOffset.UtcNow.AddHours(1))
            }
            : fixture.Invocation.Current! with { EffectiveTenantId = Guid.NewGuid() };
        var result = await fixture.Service.DeleteAsync(row.Id, row.ConcurrencyStamp);
        result.StatusCode.Should().Be(403);
        result.Message.Should().NotContain(ProtectedRegistryKeys.MutationError);
    }

    private static ChangeEntry FullChange(ChangeOperation operation, RegistryConfiguration entity) => new()
    {
        EntityTypeName = nameof(RegistryConfiguration), Operation = operation,
        SerializedEntity = MemoryPackSerializer.Serialize(entity)
    };

    private static ChangeEntry Patch(RegistryConfiguration entity, string key) => new()
    {
        EntityTypeName = nameof(RegistryConfiguration), Operation = ChangeOperation.Update,
        SerializedEntity = MemoryPackSerializer.Serialize(new FieldPatch
        {
            EntityId = MemoryPackSerializer.Serialize(entity.Id), ExpectedConcurrencyStamp = entity.ConcurrencyStamp,
            Changes = new Dictionary<string, byte[]>
            {
                [nameof(RegistryConfiguration.Key)] = MemoryPackSerializer.Serialize(key),
                [nameof(RegistryConfiguration.Value)] = MemoryPackSerializer.Serialize("replacement")
            }
        })
    };

    private sealed class Fixture : IAsyncDisposable
    {
        public Guid TenantId { get; } = Guid.NewGuid();
        public Guid GroupId { get; } = Guid.NewGuid();
        public RegistryDbContext Db { get; } = new(new DbContextOptionsBuilder<RegistryDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        public MutableInvocation Invocation { get; } = new();
        public IRegistryConfigurationService Service { get; private set; } = null!;
        private QueryExecutionService remote = null!;
        private ServiceProvider provider = null!;

        public static async Task<Fixture> CreateAsync(bool financeManager = false)
        {
            var fixture = new Fixture();
            var capabilities = new HashSet<string> { "identity:create", "identity:update", "identity:delete" };
            if (financeManager) capabilities.Add("wallets:manage");
            fixture.Invocation.Current = new TrustedInvocationContext(
                new TrustedActorIdentity(Guid.NewGuid(), null, fixture.TenantId, Guid.NewGuid(),
                    new HashSet<string>(), capabilities, "test", DateTimeOffset.UtcNow.AddHours(1)),
                new TrustedServiceIdentity(XFrameworkServiceNames.Portal, "test",
                    new HashSet<string> { XFrameworkServiceScopes.DataContextMutate }, "test"),
                fixture.TenantId, fixture.TenantId, Guid.NewGuid());
            var policies = new GeneratedEntityAuthorizationPolicyRegistry(
                new[] { GeneratedEntityOperation.Create, GeneratedEntityOperation.Update, GeneratedEntityOperation.Delete }
                    .Select(operation => new GeneratedEntityAuthorizationPolicy
                    {
                        EntityTypeName = nameof(RegistryConfiguration), Operation = operation,
                        AuthorizationFeature = "identity", RequiredCapability = $"identity:{operation.ToString().ToLowerInvariant()}",
                        TenantAccessMode = TenantAccessMode.DelegatedTenant, AllowRemoteMutation = true
                    }).Append(new GeneratedEntityAuthorizationPolicy
                    {
                        EntityTypeName = nameof(RegistryConfigurationGroup), Operation = GeneratedEntityOperation.Create,
                        AuthorizationFeature = "identity", RequiredCapability = "identity:create",
                        TenantAccessMode = TenantAccessMode.DelegatedTenant, AllowRemoteMutation = true
                    }));
            var gate = new Mock<ITrustedInvocationFeatureGate>();
            gate.Setup(x => x.EnsureGeneratedEntityAllowedAsync("identity", It.IsAny<string>(), true, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result.Success());
            var features = new Mock<ITenantModuleFeatureService>();
            features.Setup(x => x.EnsureEnabledAsync(fixture.TenantId, "identity", It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result.Success());
            var validator = new RegistryConfigurationValidator(fixture.Db);
            fixture.Service = new ProtectedRegistryConfigurationService(fixture.Db, fixture.Invocation, policies,
                gate.Object, [validator], NullLogger<RegistryConfigurationService>.Instance);
            var services = new ServiceCollection().AddSingleton<DbContext>(fixture.Db)
                .AddSingleton(features.Object);
            services.AddValidatorsFromAssemblyContaining<RegistryConfigurationValidator>();
            services.AddIdentityServerRemoteEntityValidation();
            fixture.provider = services.BuildServiceProvider();
            fixture.remote = new QueryExecutionService(fixture.provider, NullLogger<QueryExecutionService>.Instance, fixture.Invocation, policies);
            fixture.remote.RegisterEntity(typeof(RegistryConfiguration), nameof(RegistryConfiguration), allowRemoteMutation: true);
            fixture.remote.RegisterEntity(typeof(RegistryConfigurationGroup), nameof(RegistryConfigurationGroup), allowRemoteMutation: true);
            fixture.Db.Add(new RegistryConfigurationGroup
            { Id = fixture.GroupId, TenantId = fixture.TenantId, Name = "Settings", IsEnabled = true });
            await fixture.Db.SaveChangesAsync();
            fixture.Db.ChangeTracker.Clear();
            return fixture;
        }

        public async Task<RegistryConfiguration> SeedAsync(string key)
        {
            var row = new RegistryConfiguration
            {
                Id = Guid.NewGuid(), TenantId = TenantId, GroupId = GroupId, Key = key,
                Value = "original", IsEnabled = true, ConcurrencyStamp = Guid.NewGuid()
            };
            Db.Add(row);
            await Db.SaveChangesAsync();
            Db.ChangeTracker.Clear();
            return row;
        }

        public async Task<DataContextResult> ExecuteAsync(ChangeEntry change) =>
            MemoryPackSerializer.Deserialize<DataContextResult>(await remote.ExecuteChangesAsync(
                MemoryPackSerializer.Serialize(new SaveChangesRequest { Changes = [change] })))!;

        public async ValueTask DisposeAsync()
        {
            await provider.DisposeAsync();
            await Db.DisposeAsync();
        }
    }

    private sealed class MutableInvocation : ITrustedInvocationContextAccessor
    {
        public TrustedInvocationContext? Current { get; set; }
    }

    private sealed class RegistryDbContext(DbContextOptions<RegistryDbContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<RegistryConfiguration>(entity =>
            {
                entity.HasKey(x => x.Id);
                entity.Property(x => x.ConcurrencyStamp).IsConcurrencyToken();
                entity.Ignore(x => x.Tenant);
                entity.HasOne(x => x.Group).WithMany(x => x.RegistryConfigurations).HasForeignKey(x => x.GroupId);
            });
        }
    }
}
