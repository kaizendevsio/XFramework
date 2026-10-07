using System.Reflection;
using FluentAssertions;
using IdentityServer.Domain.Shared.Contracts;
using MemoryPack;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using XFramework.Domain.Shared.Attributes;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.DataContext;
using XFramework.Domain.Shared.ServiceIdentity;
using XFramework.Integration.DataContext;
using XFramework.Integration.Security;
using XFramework.Portal.Components;
using XFramework.Portal.Composition;

namespace Portal.E2ETests;

[TestFixture, NonParallelizable]
[Category("Area:PortalContract")]
public sealed class UserDetailRoleReadTests
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly FieldInfo WrapperMap =
        typeof(RemoteDataContext).GetField("_wrapperMap", BindingFlags.Static | BindingFlags.NonPublic)!;
    private static readonly Type[] Entities = [typeof(IdentityCredential), typeof(IdentityRole), typeof(IdentityRoleType)];
    private object? _originalMap;
    private ServiceProvider _services = null!;
    private RecordingWrapper _wrapper = null!;
    private object _page = null!;
    private Type _pageType = null!;
    private RequestMetadata _metadata = null!;

    [SetUp]
    public void SetUp()
    {
        _originalMap = WrapperMap.GetValue(null);
        WrapperMap.SetValue(null, Entities.ToDictionary(type => type.Name, _ => typeof(RecordingWrapper).FullName!));
        _wrapper = new RecordingWrapper();
        _services = new ServiceCollection().AddSingleton(_wrapper).BuildServiceProvider();
        _metadata = new RequestMetadata { RequestedTenantId = _wrapper.TargetTenantId, RequestId = Guid.NewGuid() };
        var owners = new[] { typeof(App).Assembly }.Concat(PortalFeatureAssemblies.All)
            .SelectMany(assembly => assembly.GetExportedTypes())
            .Where(type => type.GetCustomAttributes<RouteAttribute>()
                .Any(route => route.Template == "/identity/users/{Id:guid}/{Section}"))
            .ToArray();
        _pageType = owners.Should().ContainSingle("the regression must run the unique active router owner").Subject;
        TestContext.WriteLine($"Active user detail route: {_pageType.FullName} ({_pageType.Assembly.GetName().Name})");
        _page = Activator.CreateInstance(_pageType)!;
        var identityId = Guid.NewGuid();
        _pageType.GetProperty("Id")!.SetValue(_page, identityId);
        SetProperty("DataContext", new RemoteDataContext(_services, _metadata));
        var loggerType = typeof(ILogger<>).MakeGenericType(_pageType);
        var loggerMock = (Mock)Activator.CreateInstance(typeof(Mock<>).MakeGenericType(loggerType))!;
        SetProperty("Logger", loggerMock.Object);
        SetField("_user", new IdentityInformation { Id = identityId, TenantId = _wrapper.TargetTenantId });
    }

    [TearDown]
    public void TearDown()
    {
        WrapperMap.SetValue(null, _originalMap);
        _services.Dispose();
    }

    [Test]
    public async Task LoadActiveSectionData_RolesInManagedTenant_ReturnsExistingAssignmentWithType()
    {
        await LoadRolesSection();

        Field<bool>("_rolesLoadFailed").Should().BeFalse();
        Field<List<IdentityRole>>("_roles").Should().ContainSingle().Which.Id.Should().Be(_wrapper.Role.Id);
        Field<List<IdentityRole>>("_roles")[0].Type!.Name.Should().Be("Test POS");
        var query = _wrapper.Queries.Should().ContainSingle(query => query.EntityTypeName == nameof(IdentityRole)).Subject;
        query.Metadata!.RequestedTenantId.Should().Be(_wrapper.TargetTenantId);
        query.IgnoreQueryFilters.Should().BeTrue("the existing administrative query must remain explicitly authorized");
        query.NoCache.Should().BeTrue();
        query.Includes.Should().ContainSingle().Which.Should().Be(nameof(IdentityRole.Type));
        query.Take.Should().Be(1_000);
        query.Filters.Should().Contain(filter => filter.PropertyName == nameof(IdentityRole.TenantId));
        query.Filters.Should().Contain(filter => filter.PropertyName == nameof(IdentityRole.CredentialId));
        _wrapper.AuthorizationResults.Should().OnlyContain(result => result.IsSuccess);
        _wrapper.AuthorizationResults.Should().OnlyContain(result => result.Context!.EffectiveTenantId == _wrapper.TargetTenantId);
        _metadata.RequestedTenantId.Should().Be(_wrapper.TargetTenantId);
        _wrapper.Role.CredentialId.Should().Be(_wrapper.Credential.Id);
    }

    [TestCase(false, true)]
    [TestCase(true, false)]
    public async Task LoadRoles_MissingActorCapabilityOrScope_ShowsFailureWithoutRetry(bool capability, bool scope)
    {
        SetField("_credentials", new List<IdentityCredential> { _wrapper.Credential });
        _wrapper.HasCapability = capability;
        _wrapper.HasAllTenantsScope = scope;

        await Invoke("LoadRoles");

        Field<bool>("_rolesLoadFailed").Should().BeTrue();
        Field<List<IdentityRole>>("_roles").Should().BeEmpty();
        _wrapper.Queries.Should().ContainSingle();
        _wrapper.AuthorizationResults.Should().ContainSingle().Which.StatusCode.Should().Be(403);
    }

    [Test]
    public async Task LoadActiveSectionData_CredentialReadFails_DoesNotReportNoAssignedRoles()
    {
        _wrapper.FailCredentials = true;

        await LoadRolesSection();

        Field<bool>("_rolesLoadFailed").Should().BeTrue();
        Field<List<IdentityRole>>("_roles").Should().BeEmpty();
        _wrapper.Queries.Should().NotContain(query => query.EntityTypeName == nameof(IdentityRole));
    }

    [Test]
    public async Task LoadActiveSectionData_FailedReadThenSuccessfulRefresh_ClearsFailure()
    {
        _wrapper.FailCredentials = true;
        await LoadRolesSection();
        _wrapper.FailCredentials = false;

        await LoadRolesSection();

        Field<bool>("_rolesLoadFailed").Should().BeFalse();
        Field<List<IdentityRole>>("_roles").Should().ContainSingle();
    }

    private Task LoadRolesSection() => Invoke("LoadActiveSectionData", "roles");
    private Task Invoke(string name, params object[] arguments) =>
        (Task)_pageType.GetMethod(name, PrivateInstance)!.Invoke(_page, arguments)!;
    private T Field<T>(string name) => (T)_pageType.GetField(name, PrivateInstance)!.GetValue(_page)!;
    private void SetField(string name, object value) => _pageType.GetField(name, PrivateInstance)!.SetValue(_page, value);
    private void SetProperty(string name, object value) => _pageType.GetProperty(name, PrivateInstance)!.SetValue(_page, value);

    public sealed class RecordingWrapper : IDataContextServiceWrapper, IActorIdentityProvider, IServiceIdentityProvider
    {
        public Guid ActorTenantId { get; } = Guid.NewGuid();
        public Guid TargetTenantId { get; } = Guid.NewGuid();
        public IdentityCredential Credential { get; }
        public IdentityRole Role { get; }
        public bool HasCapability { get; set; } = true;
        public bool HasAllTenantsScope { get; set; } = true;
        public bool FailCredentials { get; set; }
        public List<QueryDescriptor> Queries { get; } = [];
        public List<TrustedInvocationResult> AuthorizationResults { get; } = [];

        public RecordingWrapper()
        {
            Credential = new IdentityCredential { Id = Guid.NewGuid(), TenantId = TargetTenantId };
            var type = new IdentityRoleType { Id = Guid.NewGuid(), TenantId = TargetTenantId, Name = "Test POS" };
            Role = new IdentityRole
            {
                Id = Guid.NewGuid(), TenantId = TargetTenantId, CredentialId = Credential.Id,
                TypeId = type.Id, Type = type, RoleExpiration = DateTime.MaxValue
            };
        }

        public async Task<byte[]> ExecuteQueryAsync(byte[] bytes, CancellationToken ct = default)
        {
            var query = MemoryPackSerializer.Deserialize<QueryDescriptor>(bytes)!;
            Queries.Add(query);
            // Use the entity's declared policy and the real resolver, not a blanket successful mock.
            var attribute = Entities.Single(type => type.Name == query.EntityTypeName)
                .GetCustomAttribute<GenerateEndpointsAttribute>()!;
            var result = await new TrustedInvocationResolver(this, this).ResolveAsync(
                new InvocationCredentials("test-actor", "test-service"), query.Metadata!,
                new InvocationAuthorizationPolicy
                {
                    ActorRequirement = ActorRequirement.Required,
                    TenantAccessMode = attribute.TenantAccessMode == GeneratedTenantAccessMode.DelegatedTenant
                        ? TenantAccessMode.DelegatedTenant : TenantAccessMode.ActorTenant,
                    RequiredActorCapabilities = query.IgnoreQueryFilters ? ["identity.tenants:manage"] : [],
                    RequiredCrossTenantActorCapabilities = ["identity.tenants:manage"],
                    RequiredServiceScopes = query.IgnoreQueryFilters
                        ? [XFrameworkServiceScopes.DataContextQuery, XFrameworkServiceScopes.DataContextQueryAllTenants]
                        : [XFrameworkServiceScopes.DataContextQuery],
                    AllowedServiceCallers = [XFrameworkServiceNames.Portal]
                }, XFrameworkServiceNames.IdentityServer, ct);
            AuthorizationResults.Add(result);
            if (!result.IsSuccess)
                throw new InvalidOperationException($"DataContext query request failed with status {result.StatusCode}.");
            if (FailCredentials && query.EntityTypeName == nameof(IdentityCredential))
                throw new InvalidOperationException("Credential read unavailable.");
            return query.EntityTypeName switch
            {
                nameof(IdentityCredential) => MemoryPackSerializer.Serialize<List<IdentityCredential>>([Credential]),
                nameof(IdentityRole) => MemoryPackSerializer.Serialize<List<IdentityRole>>([Role]),
                nameof(IdentityRoleType) => MemoryPackSerializer.Serialize<List<IdentityRoleType>>([Role.Type!]),
                _ => throw new NotSupportedException()
            };
        }

        public Task<ActorIdentityValidationResult> ValidateAsync(string token, CancellationToken ct = default) =>
            Task.FromResult(ActorIdentityValidationResult.Success(new TrustedActorIdentity(
                Guid.NewGuid(), Guid.NewGuid(), ActorTenantId, Guid.NewGuid(), new HashSet<string>(),
                HasCapability ? new HashSet<string> { "identity.tenants:manage" } : new HashSet<string>(),
                "test-generation", DateTimeOffset.UtcNow.AddMinutes(10))));

        public Task<ServiceIdentityValidationResult> ValidateAsync(string token, string expectedAudience, CancellationToken ct = default) =>
            Task.FromResult(ServiceIdentityValidationResult.Success(new TrustedServiceIdentity(
                XFrameworkServiceNames.Portal, expectedAudience,
                HasAllTenantsScope
                    ? new HashSet<string> { XFrameworkServiceScopes.DataContextQuery, XFrameworkServiceScopes.DataContextQueryAllTenants }
                    : new HashSet<string> { XFrameworkServiceScopes.DataContextQuery }, "test-generation")));

        public Task<byte[]> ExecuteChangesAsync(byte[] bytes, CancellationToken ct = default) =>
            throw new AssertionException("Role reads must not change assignments.");
        public IAsyncEnumerable<byte[]> ExecuteQueryStreamAsync(byte[] bytes, CancellationToken ct = default) =>
            throw new NotSupportedException();
    }
}
