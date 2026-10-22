using System.Reflection;
using BlazorBlueprint.Components;
using FluentAssertions;
using IdentityServer.Domain.Shared.Contracts;
using MemoryPack;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Moq;
using XFramework.Domain.Shared.Attributes;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.DataContext;
using XFramework.Domain.Shared.ServiceIdentity;
using XFramework.Integration.DataContext;
using XFramework.Integration.Security;
using XFramework.Portal.Components;
using XFramework.Portal.Composition;
using XFramework.Portal.Shared;
using XFramework.Portal.Shared.Components;

namespace Portal.E2ETests;

[TestFixture, NonParallelizable]
[Category("Area:PortalContract")]
public sealed class UserDetailRoleReadTests
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly FieldInfo WrapperMap =
        typeof(RemoteDataContext).GetField("_wrapperMap", BindingFlags.Static | BindingFlags.NonPublic)!;
    private static readonly Type[] Entities =
        [typeof(IdentityCredential), typeof(IdentityRole), typeof(IdentityRoleType), typeof(Session), typeof(AuthorizationLog)];
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

    [Test]
    public async Task LoadRoleTypes_PickerSource_IsBoundedToUserTenantAndActiveRoles()
    {
        await Invoke("LoadRoleTypes");

        var query = _wrapper.Queries.Should().ContainSingle().Subject;
        query.EntityTypeName.Should().Be(nameof(IdentityRoleType));
        query.Filters.Should().Contain(filter => filter.PropertyName == nameof(IdentityRoleType.TenantId));
        query.Filters.Single(filter => filter.PropertyName == nameof(IdentityRoleType.TenantId))
            .Value.Should().Be(_wrapper.TargetTenantId, "the user tenant, not the actor's home tenant, owns the role options");
        query.Filters.Should().Contain(filter => filter.PropertyName == nameof(IdentityRoleType.IsEnabled));
        query.Filters.Should().Contain(filter => filter.PropertyName == nameof(IdentityRoleType.IsDeleted));
        query.Take.Should().Be(100);
        query.NoCache.Should().BeTrue();
        Field<List<IdentityRoleType>>("_roleTypes").Should().ContainSingle().Which.TenantId.Should().Be(_wrapper.TargetTenantId);

        var picker = new XfEntityPicker<IdentityRoleType>
        {
            Items = Field<List<IdentityRoleType>>("_roleTypes"),
            ValueSelector = role => role.Id.ToString(),
            TextSelector = role => role.Name
        };
        foreach (var property in new[] { "FilteredItems", "AdvancedFilteredItems" })
        {
            var options = (IReadOnlyList<IdentityRoleType>)picker.GetType()
                .GetProperty(property, PrivateInstance)!.GetValue(picker)!;
            options.Should().ContainSingle().Which.TenantId.Should().Be(_wrapper.TargetTenantId);
        }
    }

    [Test]
    public async Task LoadRoleTypes_WithoutUser_DoesNotQueryAllTenants()
    {
        SetField("_user", null!);
        await Invoke("LoadRoleTypes");
        _wrapper.Queries.Should().BeEmpty();
        Field<List<IdentityRoleType>>("_roleTypes").Should().BeEmpty();
    }

    [TestCase("valid", true)]
    [TestCase("foreign-role", false)]
    [TestCase("foreign-credential", false)]
    [TestCase("disabled-role", false)]
    [TestCase("deleted-role", false)]
    [TestCase("tenant-changed", false)]
    public async Task AssignRole_SelectionMustBelongToUserTenant(string scenario, bool expected)
    {
        await LoadRolesSection();
        var roleType = Field<List<IdentityRoleType>>("_roleTypes").Single();
        var credential = Field<List<IdentityCredential>>("_credentials").Single();
        switch (scenario)
        {
            case "foreign-role": roleType.TenantId = _wrapper.ActorTenantId; break;
            case "foreign-credential": credential.TenantId = _wrapper.ActorTenantId; break;
            case "disabled-role": roleType.IsEnabled = false; break;
            case "deleted-role": roleType.IsDeleted = true; break;
            case "tenant-changed": SetField("_outsideTenantContext", true); break;
        }

        ((bool)_pageType.GetMethod("CanAssignRole", PrivateInstance)!
            .Invoke(_page, [credential.Id, roleType.Id])!).Should().Be(expected);
    }

    [TestCase("sessions", "_sessionsLoadFailed", nameof(Session))]
    [TestCase("auth-logs", "_authLogsLoadFailed", nameof(AuthorizationLog))]
    public async Task DetailAuditSection_ManagedTenant_ReturnsRows(string section, string failureField, string entity)
    {
        await Invoke("LoadActiveSectionData", section);

        Field<bool>(failureField).Should().BeFalse();
        var query = _wrapper.Queries.Should().ContainSingle(query => query.EntityTypeName == entity).Subject;
        query.Take.Should().Be(100);
        query.Filters.Should().Contain(filter => filter.PropertyName == "TenantId");
        query.Filters.Should().Contain(filter => filter.PropertyName == "CredentialId");
        _wrapper.AuthorizationResults.Should().OnlyContain(result => result.IsSuccess);
    }

    [TestCase("sessions", "_sessionsLoadFailed")]
    [TestCase("auth-logs", "_authLogsLoadFailed")]
    public async Task DetailAuditSection_CredentialFailure_IsNotAnEmptyResult(string section, string failureField)
    {
        _wrapper.FailCredentials = true;
        await Invoke("LoadActiveSectionData", section);
        Field<bool>(failureField).Should().BeTrue();

        _wrapper.FailCredentials = false;
        await Invoke("LoadActiveSectionData", section);
        Field<bool>(failureField).Should().BeFalse();
    }

    [TestCase("/identity/sessions", nameof(Session), "_sessions")]
    [TestCase("/identity/auth-logs", nameof(AuthorizationLog), "_logs")]
    public async Task AuditList_ManagedTenant_BoundedOrderedReadAndRecoverableFailure(string route, string entity, string rows)
    {
        CreateAuditPage(route, _wrapper.TargetTenantId);
        await Invoke("LoadData");

        Field<bool>("_loadFailed").Should().BeFalse();
        Field<System.Collections.ICollection>(rows).Count.Should().Be(1);
        var query = _wrapper.Queries.Should().ContainSingle(query => query.EntityTypeName == entity).Subject;
        query.Take.Should().Be(100);
        query.IgnoreQueryFilters.Should().BeFalse();
        query.Filters.Should().Contain(filter => filter.PropertyName == "TenantId");
        query.Sorting.Should().ContainSingle().Which.Should().BeEquivalentTo(
            new SortDescriptor { PropertyName = "CreatedAt", Descending = true });

        _wrapper.HasCapability = false;
        await Invoke("LoadData");
        Field<bool>("_loadFailed").Should().BeTrue();
        Field<System.Collections.ICollection>(rows).Count.Should().Be(0);
        _wrapper.AuthorizationResults.Last().StatusCode.Should().Be(403);

        _wrapper.HasCapability = true;
        await Invoke("LoadData");
        Field<bool>("_loadFailed").Should().BeFalse();
        Field<System.Collections.ICollection>(rows).Count.Should().Be(1);
    }

    [TestCase("/identity/sessions")]
    [TestCase("/identity/auth-logs")]
    public async Task AuditList_NoTenant_DoesNotQuery(string route)
    {
        CreateAuditPage(route, null);
        await Invoke("LoadData");
        _wrapper.Queries.Should().BeEmpty();
        Field<bool>("_loading").Should().BeFalse();
    }

    [TestCase("/identity/sessions", "_sessions")]
    [TestCase("/identity/auth-logs", "_logs")]
    public async Task AuditList_TenantChangesDuringRead_DiscardsOldResponse(string route, string rows)
    {
        var tenant = CreateAuditPage(route, _wrapper.TargetTenantId);
        _wrapper.ReadGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var oldLoad = Invoke("LoadData");
        tenant.SetupGet(context => context.SelectedTenantId).Returns((Guid?)null);
        await Invoke("LoadData");
        _wrapper.ReadGate.SetResult();
        await oldLoad;
        Field<System.Collections.ICollection>(rows).Count.Should().Be(0);
        Field<bool>("_loadFailed").Should().BeFalse();
        Field<bool>("_loading").Should().BeFalse();
    }

    private Mock<IPortalTenantContext> CreateAuditPage(string route, Guid? tenantId)
    {
        _pageType = PortalFeatureAssemblies.All.SelectMany(assembly => assembly.GetExportedTypes())
            .Single(type => type.GetCustomAttributes<RouteAttribute>().Any(attribute => attribute.Template == route));
        _page = Activator.CreateInstance(_pageType)!;
        SetProperty("DataContext", new RemoteDataContext(_services, _metadata));
        var tenant = new Mock<IPortalTenantContext>();
        tenant.SetupGet(context => context.SelectedTenantId).Returns(tenantId);
        SetProperty("TenantFilter", tenant.Object);
        var loggerMock = (Mock)Activator.CreateInstance(typeof(Mock<>).MakeGenericType(typeof(ILogger<>).MakeGenericType(_pageType)))!;
        SetProperty("Logger", loggerMock.Object);
        return tenant;
    }

    [TestCase("/identity/sessions", "Sessions could not be loaded.", "No sessions")]
    [TestCase("/identity/auth-logs", "Authorization logs could not be loaded.", "No authorization logs")]
    public async Task AuditList_RenderedFailure_ShowsRetryInsteadOfEmptyState(string route, string failure, string empty)
    {
        var tenant = CreateAuditPage(route, _wrapper.TargetTenantId);
        _wrapper.HasCapability = false;
        var services = new ServiceCollection().AddLogging();
        services.AddBlazorBlueprintComponents();
        services.AddSingleton<IJSRuntime>(Mock.Of<IJSRuntime>());
        services.AddSingleton(tenant.Object);
        services.AddSingleton<IDataContext>(new RemoteDataContext(_services, _metadata));
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());

        var html = await renderer.Dispatcher.InvokeAsync(async () =>
            (await renderer.RenderComponentAsync(_pageType, ParameterView.Empty)).ToHtmlString());

        html.Should().Contain(failure).And.Contain("Retry").And.NotContain(empty);
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
        public TaskCompletionSource? ReadGate { get; set; }
        public List<QueryDescriptor> Queries { get; } = [];
        public List<TrustedInvocationResult> AuthorizationResults { get; } = [];

        public RecordingWrapper()
        {
            Credential = new IdentityCredential { Id = Guid.NewGuid(), TenantId = TargetTenantId };
            var type = new IdentityRoleType { Id = Guid.NewGuid(), TenantId = TargetTenantId, Name = "Test POS", IsEnabled = true };
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
            if (ReadGate is { } gate)
                await gate.Task;
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
                nameof(Session) => MemoryPackSerializer.Serialize<List<Session>>(
                    [new Session { Id = Guid.NewGuid(), TenantId = TargetTenantId, CredentialId = Credential.Id }]),
                nameof(AuthorizationLog) => MemoryPackSerializer.Serialize<List<AuthorizationLog>>(
                    [new AuthorizationLog { Id = Guid.NewGuid(), TenantId = TargetTenantId, CredentialId = Credential.Id }]),
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
