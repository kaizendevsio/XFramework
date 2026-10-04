using System.Reflection;
using FluentAssertions;
using IdentityServer.Domain.Shared.Contracts;
using MemoryPack;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Wallets.Domain.Shared.Contracts;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.DataContext;
using XFramework.Domain.Shared.Enums;
using XFramework.Domain.Shared.ServiceIdentity;
using XFramework.Integration.DataContext;
using XFramework.Integration.Security;
using XFramework.Portal.Components.Pages;
using XFramework.Portal.Services;

namespace Portal.E2ETests;

[TestFixture, NonParallelizable]
[Category("Area:PortalContract")]
public sealed class DashboardStatisticsTests
{
    private const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly FieldInfo WrapperMap =
        typeof(RemoteDataContext).GetField("_wrapperMap", BindingFlags.Static | BindingFlags.NonPublic)!;
    private static readonly string[] Entities =
        [nameof(IdentityInformation), nameof(Session), nameof(Wallet), nameof(Tenant)];
    private readonly Guid _selectedTenantId = Guid.NewGuid();
    private object? _originalMap;
    private ServiceProvider _services = null!;
    private RecordingWrapper _wrapper = null!;
    private TenantFilterService _tenantFilter = null!;
    private RequestMetadata _metadata = null!;
    private Dashboard _page = null!;

    [SetUp]
    public void SetUp()
    {
        _originalMap = WrapperMap.GetValue(null);
        WrapperMap.SetValue(null, Entities.ToDictionary(name => name, _ => typeof(RecordingWrapper).FullName!));
        _wrapper = new RecordingWrapper();
        _services = new ServiceCollection().AddSingleton(_wrapper).BuildServiceProvider();
        _tenantFilter = new TenantFilterService();
        _tenantFilter.SetTenant(_selectedTenantId, "POS QA");
        _metadata = new RequestMetadata
        {
            RequestedTenantId = _selectedTenantId,
            RequestId = Guid.NewGuid(),
            OperationName = "Portal",
            DeviceName = "test-device",
            UserAgent = "test-agent",
            IpAddress = "127.0.0.1"
        };
        _page = new Dashboard();
        SetProperty("Services", _services);
        SetProperty("RequestMetadata", _metadata);
        SetProperty("TenantFilter", _tenantFilter);
        SetProperty("Logger", NullLogger<Dashboard>.Instance);
    }

    [TearDown]
    public void TearDown()
    {
        _page.Dispose();
        WrapperMap.SetValue(null, _originalMap);
        _services.Dispose();
    }

    [Test]
    public async Task LoadStats_ActiveTenant_UsesActorAuthorizationAndExplicitTenantFilters()
    {
        var originalRequestId = _metadata.RequestId;

        await LoadStats();

        AssertSuccessfulCounts();
        _wrapper.Queries.Select(query => query.EntityTypeName).Should().Equal(Entities);
        foreach (var query in _wrapper.Queries)
        {
            query.IgnoreQueryFilters.Should().BeTrue("the server must enforce privileged aggregate authorization");
            query.NoCache.Should().BeTrue();
            query.Mode.Should().Be(QueryExecutionMode.Count);
            query.Metadata!.RequestedTenantId.Should().BeNull("the server derives the validated actor tenant");
            query.Metadata.RequestId.Should().NotBe(originalRequestId!.Value);
            query.Metadata.OperationName.Should().Be("Portal.Dashboard");
            query.Metadata.DeviceName.Should().Be(_metadata.DeviceName);
            query.Metadata.UserAgent.Should().Be(_metadata.UserAgent);
            query.Metadata.IpAddress.Should().Be(_metadata.IpAddress);
            AssertFilter(query, nameof(Session.IsDeleted), false);
            AssertFilter(query, query.EntityTypeName == nameof(Tenant) ? nameof(Tenant.Id) : nameof(Session.TenantId),
                _selectedTenantId);
        }
        AssertFilter(_wrapper.Queries.Single(query => query.EntityTypeName == nameof(Session)),
            nameof(Session.Status), (int)CurrentSessionState.Active);
        _metadata.RequestedTenantId.Should().Be(_selectedTenantId);
        _metadata.RequestId.Should().Be(originalRequestId);
        _metadata.OperationName.Should().Be("Portal");
        _tenantFilter.SelectedTenantId.Should().Be(_selectedTenantId);
    }

    [Test]
    public async Task LoadStats_AllTenants_PreservesPrivilegedAggregateQueries()
    {
        _tenantFilter.Clear();
        _metadata.RequestedTenantId = _wrapper.ActorTenantId;

        await LoadStats();

        AssertSuccessfulCounts();
        _wrapper.Queries.Should().HaveCount(4);
        foreach (var query in _wrapper.Queries)
        {
            query.IgnoreQueryFilters.Should().BeTrue();
            query.Metadata!.RequestedTenantId.Should().BeNull();
            query.Filters.Should().NotContain(filter => filter.PropertyName == "TenantId" || filter.PropertyName == "Id");
            AssertFilter(query, nameof(Session.IsDeleted), false);
        }
    }

    [Test]
    public async Task LoadStats_TenantChangesBetweenLoads_RebuildsSelectedTenantFilters()
    {
        await LoadStats();
        _wrapper.Queries.Clear();
        var nextTenantId = Guid.NewGuid();
        _tenantFilter.SetTenant(nextTenantId, "Next tenant");
        _metadata.RequestedTenantId = nextTenantId;

        await LoadStats();

        AssertSuccessfulCounts();
        foreach (var query in _wrapper.Queries)
        {
            AssertFilter(query, query.EntityTypeName == nameof(Tenant) ? "Id" : "TenantId", nextTenantId);
            query.Metadata!.RequestedTenantId.Should().BeNull();
        }
        _metadata.RequestedTenantId.Should().Be(nextTenantId);
    }

    [TestCase(false, true)]
    [TestCase(true, false)]
    public async Task LoadStats_MissingAggregateAuthorization_RemainsUnavailable(bool capability, bool scope)
    {
        _wrapper.HasCapability = capability;
        _wrapper.HasAllTenantsScope = scope;

        await LoadStats();

        Field<string>("_error").Should().Be("Dashboard statistics are temporarily unavailable. Please try again.");
        Field<bool>("_loading").Should().BeFalse();
        _wrapper.Queries.Should().ContainSingle("a rejected request must not retry with weaker authorization");
        _wrapper.AuthorizationResults.Should().ContainSingle().Which.StatusCode.Should().Be(403);
        _metadata.RequestedTenantId.Should().Be(_selectedTenantId);
    }

    [Test]
    public async Task SessionQuery_WithSharedActiveTenantMetadata_ReproducesActorTenantRejection()
    {
        var query = new RemoteDataContext(_services, _metadata).Query<Session>()
            .IgnoreQueryFilters().Where(session => session.TenantId == _selectedTenantId);

        var action = async () => await query.CountAsync();

        await action.Should().ThrowAsync<InvalidOperationException>();
        var result = _wrapper.AuthorizationResults.Should().ContainSingle().Subject;
        result.StatusCode.Should().Be(403);
        result.Error.Should().Be("Requested tenant does not match the authenticated actor tenant.");
    }

    private void AssertSuccessfulCounts()
    {
        Field<string?>("_error").Should().BeNull();
        Field<bool>("_loading").Should().BeFalse();
        Field<long>("_userCount").Should().Be(10);
        Field<long>("_sessionCount").Should().Be(11);
        Field<long>("_walletCount").Should().Be(12);
        Field<long>("_tenantCount").Should().Be(13);
        _wrapper.AuthorizationResults.Should().OnlyContain(result => result.IsSuccess);
    }

    private static void AssertFilter(QueryDescriptor query, string property, object value)
    {
        var filter = query.Filters.Should().ContainSingle(filter => filter.PropertyName == property).Subject;
        filter.Operation.Should().Be(QueryFilterOperation.Equal);
        filter.Value.Should().Be(value);
    }

    private void SetProperty(string name, object value) =>
        typeof(Dashboard).GetProperty(name, InstanceFlags)!.SetValue(_page, value);

    private T Field<T>(string name) => (T)typeof(Dashboard).GetField(name, InstanceFlags)!.GetValue(_page)!;

    private Task LoadStats() => (Task)typeof(Dashboard).GetMethod("LoadStats", InstanceFlags)!.Invoke(_page, null)!;

    private sealed class RecordingWrapper : IDataContextServiceWrapper, IActorIdentityProvider, IServiceIdentityProvider
    {
        public Guid ActorTenantId { get; } = Guid.NewGuid();
        public bool HasCapability { get; set; } = true;
        public bool HasAllTenantsScope { get; set; } = true;
        public List<QueryDescriptor> Queries { get; } = [];
        public List<TrustedInvocationResult> AuthorizationResults { get; } = [];

        public async Task<byte[]> ExecuteQueryAsync(byte[] bytes, CancellationToken ct = default)
        {
            var query = MemoryPackSerializer.Deserialize<QueryDescriptor>(bytes)!;
            Queries.Add(query);
            // Exercise the real resolver with the actor-tenant policy used by Session and Tenant.
            var result = await new TrustedInvocationResolver(this, this).ResolveAsync(
                new InvocationCredentials("test-actor", "test-service"), query.Metadata!,
                new InvocationAuthorizationPolicy
                {
                    RequireServiceIdentity = true,
                    TenantAccessMode = TenantAccessMode.ActorTenant,
                    RequiredActorCapabilities = query.IgnoreQueryFilters ? ["identity.tenants:manage"] : [],
                    RequiredServiceScopes = query.IgnoreQueryFilters
                        ? [XFrameworkServiceScopes.DataContextQuery, XFrameworkServiceScopes.DataContextQueryAllTenants]
                        : [XFrameworkServiceScopes.DataContextQuery],
                    AllowedServiceCallers = [XFrameworkServiceNames.Portal]
                }, "test-audience", ct);
            AuthorizationResults.Add(result);
            if (!result.IsSuccess)
                throw new InvalidOperationException($"Remote query failed: {result.StatusCode} {result.Error}");
            return MemoryPackSerializer.Serialize(10 + Array.IndexOf(Entities, query.EntityTypeName));
        }

        public Task<ActorIdentityValidationResult> ValidateAsync(string token, CancellationToken ct = default) =>
            Task.FromResult(ActorIdentityValidationResult.Success(new TrustedActorIdentity(
                Guid.NewGuid(), Guid.NewGuid(), ActorTenantId, Guid.NewGuid(),
                new HashSet<string> { "superadmin" },
                HasCapability ? new HashSet<string> { "identity.tenants:manage" } : new HashSet<string>(),
                "test-generation", DateTimeOffset.UtcNow.AddMinutes(10))));

        public Task<ServiceIdentityValidationResult> ValidateAsync(
            string token, string expectedAudience, CancellationToken ct = default) =>
            Task.FromResult(ServiceIdentityValidationResult.Success(new TrustedServiceIdentity(
                XFrameworkServiceNames.Portal, expectedAudience,
                HasAllTenantsScope
                    ? new HashSet<string> { XFrameworkServiceScopes.DataContextQuery, XFrameworkServiceScopes.DataContextQueryAllTenants }
                    : new HashSet<string> { XFrameworkServiceScopes.DataContextQuery },
                "test-generation")));

        public Task<byte[]> ExecuteChangesAsync(byte[] bytes, CancellationToken ct = default) =>
            throw new NotSupportedException("Dashboard statistics must not mutate data.");

        public IAsyncEnumerable<byte[]> ExecuteQueryStreamAsync(byte[] bytes, CancellationToken ct = default) =>
            throw new NotSupportedException("Dashboard statistics use scalar queries.");
    }
}
