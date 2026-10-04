using IdentityServer.Api.Services;
using IdentityServer.Domain.Shared.Contracts;
using IdentityServer.Domain.Shared.Contracts.Requests;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using Npgsql;
using XFramework.Core.DataContext;
using XFramework.Domain.Contexts;
using XFramework.Domain.Shared.DataContext;
using XFramework.Domain.Shared.Security;
using XFramework.TestInfrastructure;

namespace IdentityServer.IntegrationTests.Tests;

[TestFixture]
[NonParallelizable]
[Category(TestCategories.Integration)]
[Category(TestCategories.IdentityServer)]
public sealed class TenantModuleFeatureConcurrencyTests : IntegrationTestBase
{
    [Test]
    public async Task SetTenantModuleFeatures_ConcurrentInitialization_ReturnsConflictWithoutDuplicateInsert()
    {
        var tenantId = Guid.NewGuid();
        var stamp = Guid.NewGuid();
        await using (var seedDb = CreateDbContext())
        {
            seedDb.Add(new Tenant
            {
                Id = tenantId, TenantId = tenantId, Name = $"Concurrent features {tenantId:N}",
                Version = 1, Status = 1, IsEnabled = true, ConcurrencyStamp = stamp
            });
            await seedDb.SaveChangesAsync();
        }

        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var arrivals = 0;
        async Task BeforeFirstSave(CancellationToken ct)
        {
            if (Interlocked.Increment(ref arrivals) == 2)
                ready.SetResult();
            await ready.Task.WaitAsync(TimeSpan.FromSeconds(30), ct);
        }

        var databaseErrors = new System.Collections.Concurrent.ConcurrentBag<Exception>();
        async Task<XFramework.Core.Patterns.Result> Initialize()
        {
            using var scope = IntegrationTestFixture.Services.CreateScope();
            IntegrationTestFixture.EstablishTrustedActorContext(
                scope.ServiceProvider, IntegrationTestFixture.TestTenantId, IntegrationTestFixture.TestCredentialId,
                capabilities: new HashSet<string>(["identity.tenants:manage"]));
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var log = new Mock<ILogger<ServerDataContext<AppDbContext>>>();
            log.Setup(logger => logger.Log(
                    It.IsAny<LogLevel>(), It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception?>(), It.IsAny<Func<It.IsAnyType, Exception?, string>>()))
                .Callback(new InvocationAction(invocation =>
                {
                    if (invocation.Arguments[3] is Exception exception)
                        databaseErrors.Add(exception);
                }));
            var data = new GatedDataContext(new ServerDataContext<AppDbContext>(db, log.Object), BeforeFirstSave);
            var service = ActivatorUtilities.CreateInstance<IdentityAuthorizationService>(scope.ServiceProvider, data);
            return await service.SetTenantModuleFeaturesAsync(new SetTenantModuleFeaturesRequest
            {
                TenantId = tenantId,
                ExpectedConcurrencyStamp = stamp,
                Features = TenantModuleFeatureKeys.All.Select(definition => new TenantModuleFeatureUpdate
                {
                    ModuleKey = definition.ModuleKey,
                    SubFeatureKey = definition.SubFeatureKey,
                    IsEnabled = definition.DefaultEnabled
                }).ToList()
            });
        }

        var results = await Task.WhenAll(Initialize(), Initialize());
        TestContext.Out.WriteLine($"Statuses: {string.Join(", ", results.Select(result => result.StatusCode))}");
        foreach (var exception in databaseErrors)
            TestContext.Out.WriteLine(exception.GetBaseException().Message);

        results.Count(result => result.IsSuccess).Should().Be(1);
        results.Single(result => !result.IsSuccess).StatusCode.Should().Be(409);
        databaseErrors.Select(exception => exception.GetBaseException()).OfType<PostgresException>()
            .Should().NotContain(exception => exception.SqlState == PostgresErrorCodes.UniqueViolation);
        await using var verificationDb = CreateDbContext();
        var rows = await verificationDb.Set<TenantModuleFeature>().IgnoreQueryFilters()
            .Where(row => row.TenantId == tenantId).ToListAsync();
        rows.Should().HaveCount(TenantModuleFeatureKeys.All.Count);
        rows.Select(row => row.Key).Should().OnlyHaveUniqueItems();
    }

    [Test]
    public async Task SetTenantModuleFeatures_FeatureSaveFails_RollsBackTenantVersionAndFeatures()
    {
        var tenantId = Guid.NewGuid();
        var stamp = Guid.NewGuid();
        await using (var seedDb = CreateDbContext())
        {
            seedDb.Add(new Tenant
            {
                Id = tenantId, TenantId = tenantId, Name = $"Feature rollback {tenantId:N}",
                Version = 1, Status = 1, IsEnabled = true, ConcurrencyStamp = stamp
            });
            await seedDb.SaveChangesAsync();
        }

        using var scope = IntegrationTestFixture.Services.CreateScope();
        IntegrationTestFixture.EstablishTrustedActorContext(
            scope.ServiceProvider, IntegrationTestFixture.TestTenantId, IntegrationTestFixture.TestCredentialId,
            capabilities: new HashSet<string>(["identity.tenants:manage"]));
        var failure = new FailFeatureSaveInterceptor();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(IntegrationTestFixture.ConnectionString)
            .AddInterceptors(failure)
            .Options;
        await using var db = new AppDbContext(
            options,
            scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>(),
            scope.ServiceProvider.GetRequiredService<IConfiguration>(),
            scope.ServiceProvider.GetRequiredService<IEffectiveTenantContextAccessor>(),
            scope.ServiceProvider.GetRequiredService<ICrossTenantWriteAuthorizationAccessor>());
        var service = ActivatorUtilities.CreateInstance<IdentityAuthorizationService>(
            scope.ServiceProvider, db, new ServerDataContext<AppDbContext>(db));
        var result = await service.SetTenantModuleFeaturesAsync(new SetTenantModuleFeaturesRequest
        {
            TenantId = tenantId,
            ExpectedConcurrencyStamp = stamp,
            Features =
            [
                new() { ModuleKey = TenantModuleFeatureKeys.Wallets, IsEnabled = true },
                new() { ModuleKey = TenantModuleFeatureKeys.Inventario, IsEnabled = true }
            ]
        });

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(400);
        failure.Injected.Should().BeTrue();
        await using var verificationDb = CreateDbContext();
        var tenant = await verificationDb.Set<Tenant>().IgnoreQueryFilters().SingleAsync(row => row.Id == tenantId);
        tenant.ConcurrencyStamp.Should().Be(stamp);
        (await verificationDb.Set<TenantModuleFeature>().IgnoreQueryFilters()
            .AnyAsync(row => row.TenantId == tenantId)).Should().BeFalse();
    }

    private sealed class FailFeatureSaveInterceptor : SaveChangesInterceptor
    {
        public bool Injected { get; private set; }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (eventData.Context!.ChangeTracker.Entries<TenantModuleFeature>().Any())
            {
                Injected = true;
                throw new DbUpdateException("Injected feature persistence failure");
            }
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }

    private sealed class GatedDataContext(IDataContext inner, Func<CancellationToken, Task> beforeFirstSave) : IDataContext
    {
        private bool _saved;

        public IRemoteQuery<T> Query<T>() where T : class => inner.Query<T>();
        public void Add<T>(T entity) where T : class => inner.Add(entity);
        public void Update<T>(T entity) where T : class => inner.Update(entity);
        public void Remove<T>(T entity) where T : class => inner.Remove(entity);

        public async Task<DataContextResult> SaveChangesAsync(CancellationToken ct = default)
        {
            // Hold both requests after their reads, before either can commit initialization.
            if (!_saved)
            {
                _saved = true;
                await beforeFirstSave(ct);
            }
            return await inner.SaveChangesAsync(ct);
        }
    }
}
