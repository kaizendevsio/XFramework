using System.Reflection;
using System.Security.Claims;
using BlazorBlueprint.Components;
using FluentAssertions;
using IdentityServer.Domain.Shared.Contracts;
using IdentityServer.Integration.Drivers;
using MemoryPack;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Moq;
using XFramework.Core.DataContext;
using XFramework.Domain.Shared.Attributes;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.DataContext;
using XFramework.Domain.Shared.ServiceIdentity;
using XFramework.Integration.DataContext;
using XFramework.Integration.Security;
using XFramework.Portal.Features.Identity.Components;
using XFramework.Portal.Shared;

namespace Portal.E2ETests;

[TestFixture, NonParallelizable]
[Category("Area:PortalContract")]
public sealed class TenantRoleTypesCreationTests
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly FieldInfo WrapperMap = typeof(RemoteDataContext)
        .GetField("_wrapperMap", BindingFlags.Static | BindingFlags.NonPublic)!;
    private object? _originalMap;
    private ServiceProvider _services = null!;
    private TenantRoleTypes _component = null!;
    private Mock<IIdentityServerServiceWrapper> _wrapper = null!;
    private Mock<IPortalTenantContext> _tenantContext = null!;
    private RecordingNavigation _navigation = null!;
    private TestIdentities _identities = null!;
    private readonly List<QueryDescriptor> _queries = [];
    private readonly List<SaveChangesRequest> _writes = [];
    private readonly List<IdentityRoleType> _roles = [];
    private readonly List<IdentityRoleTypeGroup> _groups = [];
    private Guid _tenantId;
    private Guid? _profileTenantId;
    private DataContextResult? _failure;
    private TaskCompletionSource<bool>? _writeGate;
    private bool _failReads;

    [SetUp]
    public void SetUp()
    {
        _originalMap = WrapperMap.GetValue(null);
        WrapperMap.SetValue(null, new Dictionary<string, string>
        {
            [nameof(IdentityRoleType)] = typeof(IIdentityServerServiceWrapper).FullName!,
            [nameof(IdentityRoleTypeGroup)] = typeof(IIdentityServerServiceWrapper).FullName!
        });
        _queries.Clear(); _writes.Clear(); _roles.Clear(); _groups.Clear();
        _failure = null; _writeGate = null; _failReads = false;
        _tenantId = Guid.NewGuid();
        _profileTenantId = null;
        _identities = new TestIdentities();
        _groups.Add(new IdentityRoleTypeGroup
        {
            Id = Guid.NewGuid(), TenantId = _tenantId, Name = "Operators", IsEnabled = true
        });
        _wrapper = new Mock<IIdentityServerServiceWrapper>(MockBehavior.Strict);
        _wrapper.Setup(wrapper => wrapper.ExecuteQueryAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .Returns((byte[] bytes, CancellationToken _) => ReadAsync(bytes));
        _wrapper.Setup(wrapper => wrapper.ExecuteChangesAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .Returns((byte[] bytes, CancellationToken _) => WriteAsync(bytes));
        _tenantContext = new Mock<IPortalTenantContext>();
        _tenantContext.SetupGet(context => context.SelectedTenantId).Returns(() => _profileTenantId);
        _navigation = new RecordingNavigation();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddBlazorBlueprintComponents();
        services.AddSingleton<IJSRuntime, NoBrowserJavaScript>();
        services.AddSingleton<NavigationManager>(_navigation);
        services.AddSingleton(_wrapper.Object);
        services.AddSingleton(_tenantContext.Object);
        _services = services.BuildServiceProvider();
        _component = new TenantRoleTypes { TenantId = _tenantId, TenantName = "Target Tenant" };
        SetProperty("Services", _services);
        SetProperty("IdentityServer", _wrapper.Object);
        SetProperty("TenantContext", _tenantContext.Object);
        SetProperty("Navigation", _navigation);
        SetProperty("ToastService", _services.GetRequiredService<ToastService>());
        SetProperty("AuthenticationStateTask", AuthState());
    }

    [TearDown]
    public async Task TearDown()
    {
        _component.Dispose();
        WrapperMap.SetValue(null, _originalMap);
        await _services.DisposeAsync();
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task SaveRole_AllTenantsOrOtherProfile_TargetsRouteTenantAndOpensCapabilities(bool otherProfile)
    {
        _profileTenantId = otherProfile ? Guid.NewGuid() : null;
        await LoadAsync();
        OpenValidRole();

        await InvokeAsync("SaveRoleAsync");

        _writes.Should().ContainSingle();
        _identities.Results.Should().ContainSingle().Which.IsSuccess.Should().BeTrue();
        _identities.Results.Single().StatusCode.Should().Be(200);
        var request = _writes.Single();
        request.Metadata!.RequestedTenantId.Should().Be(_tenantId);
        request.Changes.Should().ContainSingle().Which.Operation.Should().Be(ChangeOperation.Add);
        var role = MemoryPackSerializer.Deserialize<IdentityRoleType>(request.Changes.Single().SerializedEntity)!;
        role.TenantId.Should().Be(_tenantId);
        role.GroupId.Should().Be(_groups.First().Id);
        role.Name.Should().Be("Cashier");
        role.IsEnabled.Should().BeTrue();
        Field<List<IdentityRoleType>>("_roles").Should().ContainSingle().Which.Should().BeEquivalentTo(role);
        Field<bool>("_roleOpen").Should().BeFalse();
        _navigation.LastUri.Should().Be($"/identity/tenants/{_tenantId}/role-types/{role.Id}");
        _queries.Should().HaveCount(4, "saving refreshes both lists before navigation");
        _queries.Should().OnlyContain(query => query.Metadata!.RequestedTenantId == _tenantId);
        _queries.Should().OnlyContain(query => query.IgnoreQueryFilters && query.Take == 1_000);
        foreach (var query in _queries)
            query.Filters.Should().Contain(filter => filter.PropertyName == nameof(IdentityRoleType.TenantId));
    }

    [Test]
    public async Task SaveGroup_FromRoleForm_PreservesDraftAndSelectsNewGroup()
    {
        await LoadAsync();
        OpenValidRole();
        Invoke("OpenGroupDialog", true);
        Field<bool>("_roleOpen").Should().BeFalse("only one focused dialog is open");
        SetField("_groupName", "  Store Staff  ");
        SetField("_groupDescription", "Operations");

        await InvokeAsync("SaveGroupAsync");

        _writes.Should().ContainSingle();
        var request = _writes.Single();
        var group = MemoryPackSerializer.Deserialize<IdentityRoleTypeGroup>(request.Changes.Single().SerializedEntity)!;
        group.TenantId.Should().Be(_tenantId);
        group.Name.Should().Be("Store Staff");
        request.Metadata!.RequestedTenantId.Should().Be(_tenantId);
        Field<string>("_groupId").Should().Be(group.Id.ToString());
        Field<string>("_roleName").Should().Be("  Cashier  ");
        Field<bool>("_roleOpen").Should().BeTrue();
        Field<bool>("_groupOpen").Should().BeFalse();
        Field<List<IdentityRoleTypeGroup>>("_groups").Should().Contain(item => item.Id == group.Id);
        _navigation.LastUri.Should().BeNull();

        await InvokeAsync("SaveRoleAsync");
        var role = MemoryPackSerializer.Deserialize<IdentityRoleType>(_writes.Last().Changes.Single().SerializedEntity)!;
        role.GroupId.Should().Be(group.Id);
    }

    [Test]
    public async Task SaveGroup_FromToolbar_CreatesWithoutOpeningRoleForm()
    {
        await LoadAsync();
        Invoke("OpenGroupDialog", false);
        SetField("_groupName", "Managers");

        await InvokeAsync("SaveGroupAsync");

        _writes.Should().ContainSingle();
        Field<bool>("_roleOpen").Should().BeFalse();
        Field<bool>("_groupOpen").Should().BeFalse();
    }

    [Test]
    public async Task CancelGroup_FromRoleForm_RestoresRoleDraftWithoutWriting()
    {
        await LoadAsync();
        OpenValidRole();
        Invoke("OpenGroupDialog", true);
        Invoke("SetGroupOpen", false);

        Field<bool>("_roleOpen").Should().BeTrue();
        Field<string>("_roleName").Should().Be("  Cashier  ");
        _writes.Should().BeEmpty();
    }

    [TestCase("")]
    [TestCase("   ")]
    [TestCase("over-limit")]
    public async Task SaveRole_InvalidName_DoesNotWrite(string name)
    {
        await LoadAsync();
        OpenValidRole();
        SetField("_roleName", name == "over-limit" ? new string('x', 101) : name);

        await InvokeAsync("SaveRoleAsync");

        _writes.Should().BeEmpty();
        Field<string>("_roleError").Should().Contain("Name");
        Field<bool>("_roleOpen").Should().BeTrue();
    }

    [TestCase("missing")]
    [TestCase("other-tenant")]
    [TestCase("disabled")]
    [TestCase("deleted")]
    public async Task SaveRole_InvalidGroup_DoesNotWrite(string scenario)
    {
        await LoadAsync();
        OpenValidRole();
        var group = Field<List<IdentityRoleTypeGroup>>("_groups").First();
        if (scenario == "missing") SetField("_groupId", "");
        if (scenario == "other-tenant") group.TenantId = Guid.NewGuid();
        if (scenario == "disabled") group.IsEnabled = false;
        if (scenario == "deleted") group.IsDeleted = true;

        await InvokeAsync("SaveRoleAsync");

        _writes.Should().BeEmpty();
        Field<string>("_roleError").Should().Contain("active role group");
    }

    [Test]
    public async Task SaveGroup_EmptyName_DoesNotWrite()
    {
        await LoadAsync();
        Invoke("OpenGroupDialog", false);
        await InvokeAsync("SaveGroupAsync");
        _writes.Should().BeEmpty();
        Field<string>("_groupError").Should().Be("Name is required.");
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task SaveRole_TenantRouteOrProfileChanged_DoesNotWrite(bool routeChanged)
    {
        await LoadAsync();
        OpenValidRole();
        if (routeChanged) _component.TenantId = Guid.NewGuid();
        else _profileTenantId = Guid.NewGuid();

        await InvokeAsync("SaveRoleAsync");

        _writes.Should().BeEmpty();
        _navigation.LastUri.Should().BeNull();
    }

    [Test]
    public async Task SaveRole_ProfileChangedAndChangedBack_InvalidatedDraftDoesNotWrite()
    {
        await LoadAsync();
        OpenValidRole();
        Invoke("InvalidateDrafts");

        await InvokeAsync("SaveRoleAsync");

        _writes.Should().BeEmpty();
        Field<bool>("_roleOpen").Should().BeFalse();
    }

    [Test]
    public async Task SaveRole_AccessRevokedAfterOpening_DoesNotWrite()
    {
        await LoadAsync();
        OpenValidRole();
        SetProperty("AuthenticationStateTask", AuthState(false));

        await InvokeAsync("SaveRoleAsync");

        _writes.Should().BeEmpty();
    }

    [Test]
    public async Task Load_OrdinaryActor_DoesNotReadOrOfferCreation()
    {
        SetProperty("AuthenticationStateTask", AuthState(false));
        await LoadAsync();
        Invoke("OpenRoleDialog");
        Invoke("OpenGroupDialog", false);

        _queries.Should().BeEmpty();
        Field<bool>("_roleOpen").Should().BeFalse();
        Field<bool>("_groupOpen").Should().BeFalse();
    }

    [Test]
    public async Task Load_SuperUser_KeepsExistingTenantManagementPermission()
    {
        SetProperty("AuthenticationStateTask", Task.FromResult(new AuthenticationState(new ClaimsPrincipal(
            new ClaimsIdentity([new Claim(PortalAuthClaims.IsSuperUser, "true")], "test")))));

        await LoadAsync();
        Invoke("OpenRoleDialog");

        Field<bool>("_canManage").Should().BeTrue();
        Field<bool>("_roleOpen").Should().BeTrue();
        _queries.Should().HaveCount(2);
    }

    [TestCase("actor")]
    [TestCase("delegation")]
    [TestCase("scope")]
    [TestCase("create-capability")]
    public async Task SaveRole_TrustedAuthorizationRejectsActorOrScope_KeepsDialogWithSafeError(string missing)
    {
        await LoadAsync();
        OpenValidRole();
        _identities.HasActor = missing != "actor";
        _identities.HasDelegation = missing != "delegation";
        _identities.HasMutationScope = missing != "scope";
        _identities.HasRoleCreate = missing != "create-capability";

        await InvokeAsync("SaveRoleAsync");

        _writes.Should().ContainSingle();
        _identities.Results.Should().ContainSingle().Which.IsSuccess.Should().BeFalse();
        _identities.Results.Single().StatusCode.Should().Be(missing == "actor" ? 401 : 403);
        _identities.Results.Single().Error.Should().NotBeNullOrEmpty();
        _roles.Should().BeEmpty();
        Field<bool>("_roleOpen").Should().BeTrue();
        Field<string>("_roleError").Should().Contain("not authorized");
        _navigation.LastUri.Should().BeNull();
    }

    [TestCase(400)]
    [TestCase(409)]
    [TestCase(500)]
    public async Task SaveGroup_ServerFailure_KeepsDraftAndDoesNotExposeServerDetails(int status)
    {
        await LoadAsync();
        Invoke("OpenGroupDialog", false);
        SetField("_groupName", "Managers");
        _failure = DataContextResult.Failure("SQL token secret", status);

        await InvokeAsync("SaveGroupAsync");

        Field<bool>("_groupOpen").Should().BeTrue();
        Field<string>("_groupError").Should().NotBeNullOrEmpty().And.NotContain("secret");
        Field<bool>("_saving").Should().BeFalse();
        _groups.Should().ContainSingle();
    }

    [Test]
    public async Task SaveRole_DoubleClickAndContextChangeDuringWrite_DoesNotDuplicateOrNavigate()
    {
        await LoadAsync();
        OpenValidRole();
        _writeGate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var pending = InvokeAsync("SaveRoleAsync");
        try
        {
            await InvokeAsync("SaveRoleAsync");
            _writes.Should().ContainSingle();
            Field<bool>("_saving").Should().BeTrue();
            Invoke("InvalidateDrafts");
        }
        finally
        {
            _writeGate.SetResult(true);
            await pending;
        }

        _navigation.LastUri.Should().BeNull();
        Field<List<IdentityRoleType>>("_roles").Should().BeEmpty();
        Field<bool>("_saving").Should().BeFalse();
    }

    [Test]
    public async Task Reload_ReadFailure_BlocksCreationUntilSuccessfulReload()
    {
        _failReads = true;
        await LoadAsync();
        Invoke("OpenRoleDialog");
        Field<bool>("_loadFailed").Should().BeTrue();
        Field<bool>("_roleOpen").Should().BeFalse();

        _failReads = false;
        await InvokeAsync("ReloadRolesAsync");
        Invoke("OpenRoleDialog");
        Field<bool>("_loadFailed").Should().BeFalse();
        Field<bool>("_roleOpen").Should().BeTrue();
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task Render_ActorAccess_ShowsCreateActionsAndNativeFiltersOnlyForAdministrator(bool administrator)
    {
        _roles.Add(new IdentityRoleType
        {
            Id = Guid.NewGuid(), TenantId = _tenantId, Name = "Existing Role",
            GroupId = _groups.First().Id, IsEnabled = true
        });
        await using var renderer = new HtmlRenderer(_services, _services.GetRequiredService<ILoggerFactory>());
        var html = await renderer.Dispatcher.InvokeAsync(async () =>
            (await renderer.RenderComponentAsync<RoleHost>(ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                [nameof(RoleHost.TenantId)] = _tenantId,
                [nameof(RoleHost.Authentication)] = AuthState(administrator)
            }))).ToHtmlString());

        if (administrator)
        {
            html.Should().Contain("Add Role Type").And.Contain("Add Role Group");
            html.Should().Contain("Filter Name").And.Contain("Filter Group").And.Contain("Filter Enabled");
            html.Should().Contain("Existing Role").And.Contain("Configure role capabilities");
            html.Should().NotContain("Filter Actions");
        }
        else
        {
            html.Should().Contain("Tenant Management Restricted");
            html.Should().NotContain("Add Role Type").And.NotContain("Add Role Group");
        }
        _writes.Should().BeEmpty();
    }

    private Task LoadAsync() => InvokeAsync("OnParametersSetAsync");
    private object? Invoke(string method, params object[] args) =>
        typeof(TenantRoleTypes).GetMethod(method, PrivateInstance)!.Invoke(_component, args);
    private Task InvokeAsync(string method) => (Task)Invoke(method)!;
    private void SetField(string name, object value) => typeof(TenantRoleTypes).GetField(name, PrivateInstance)!.SetValue(_component, value);
    private T Field<T>(string name) => (T)typeof(TenantRoleTypes).GetField(name, PrivateInstance)!.GetValue(_component)!;
    private void SetProperty(string name, object value) => typeof(TenantRoleTypes).GetProperty(name, PrivateInstance)!.SetValue(_component, value);

    private void OpenValidRole()
    {
        Invoke("OpenRoleDialog");
        SetField("_roleName", "  Cashier  ");
        SetField("_groupId", _groups.First().Id.ToString());
    }

    private Task<byte[]> ReadAsync(byte[] bytes)
    {
        var query = MemoryPackSerializer.Deserialize<QueryDescriptor>(bytes)!;
        _queries.Add(query);
        if (_failReads) throw new InvalidOperationException("Unavailable");
        return Task.FromResult(query.EntityTypeName switch
        {
            nameof(IdentityRoleType) => MemoryPackSerializer.Serialize(_roles.ToList()),
            nameof(IdentityRoleTypeGroup) => MemoryPackSerializer.Serialize(_groups.ToList()),
            _ => throw new AssertionException("Unexpected entity read")
        });
    }

    private async Task<byte[]> WriteAsync(byte[] bytes)
    {
        var request = MemoryPackSerializer.Deserialize<SaveChangesRequest>(bytes)!;
        _writes.Add(request);
        var change = request.Changes.Single();
        var type = change.EntityTypeName == nameof(IdentityRoleType) ? typeof(IdentityRoleType) : typeof(IdentityRoleTypeGroup);
        var authorization = await _identities.AuthorizeAsync(type, request.Metadata!);
        if (!authorization.IsSuccess)
            return MemoryPackSerializer.Serialize(DataContextResult.Failure(authorization.Error!, authorization.StatusCode));
        if (_writeGate is not null) await _writeGate.Task;
        if (_failure is not null) return MemoryPackSerializer.Serialize(_failure);
        if (type == typeof(IdentityRoleType))
            _roles.Add(MemoryPackSerializer.Deserialize<IdentityRoleType>(change.SerializedEntity)!);
        else
            _groups.Add(MemoryPackSerializer.Deserialize<IdentityRoleTypeGroup>(change.SerializedEntity)!);
        return MemoryPackSerializer.Serialize(DataContextResult.Success());
    }

    private static Task<AuthenticationState> AuthState(bool administrator = true) =>
        Task.FromResult(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity(
            administrator ? [new Claim(PortalAuthClaims.RoleTypeId, PortalAccess.AdminRoleTypeId.ToString())] : [], "test"))));

    public sealed class RoleHost : ComponentBase
    {
        [Parameter] public Guid TenantId { get; set; }
        [Parameter] public Task<AuthenticationState> Authentication { get; set; } = null!;
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenComponent<CascadingValue<Task<AuthenticationState>>>(0);
            builder.AddAttribute(1, "Value", Authentication);
            builder.AddAttribute(2, "ChildContent", (RenderFragment)(child =>
            {
                child.OpenComponent<TenantRoleTypes>(0);
                child.AddAttribute(1, "TenantId", TenantId);
                child.AddAttribute(2, "TenantName", "Target Tenant");
                child.CloseComponent();
            }));
            builder.CloseComponent();
        }
    }

    private sealed class TestIdentities : IActorIdentityProvider, IServiceIdentityProvider
    {
        public bool HasActor { get; set; } = true;
        public bool HasDelegation { get; set; } = true;
        public bool HasMutationScope { get; set; } = true;
        public bool HasRoleCreate { get; set; } = true;
        public Guid ActorTenantId { get; } = Guid.NewGuid();
        public List<InvocationPolicyCheckResult> Results { get; } = [];

        public async Task<InvocationPolicyCheckResult> AuthorizeAsync(Type entityType, RequestMetadata metadata)
        {
            var attribute = entityType.GetCustomAttribute<GenerateEndpointsAttribute>()!;
            entityType.GetCustomAttribute<AllowRemoteDataContextMutationAttribute>().Should().NotBeNull();
            attribute.TenantAccessMode.Should().Be(GeneratedTenantAccessMode.DelegatedTenant);
            var resolved = await new TrustedInvocationResolver(this, this).ResolveAsync(
                new InvocationCredentials(HasActor ? "actor" : null, "service"), metadata,
                new InvocationAuthorizationPolicy
                {
                    ActorRequirement = ActorRequirement.Required,
                    TenantAccessMode = TenantAccessMode.DelegatedTenant,
                    RequiredCrossTenantActorCapabilities = ["identity.tenants:manage"],
                    RequiredServiceScopes = [XFrameworkServiceScopes.DataContextMutate],
                    AllowedServiceCallers = [XFrameworkServiceNames.Portal]
                }, XFrameworkServiceNames.IdentityServer);
            var result = resolved.IsSuccess
                ? GeneratedEntityAuthorizationEvaluator.Evaluate(resolved.Context, new GeneratedEntityAuthorizationPolicy
                {
                    EntityTypeName = entityType.Name, Operation = GeneratedEntityOperation.Create,
                    TenantAccessMode = TenantAccessMode.DelegatedTenant,
                    RequiredCapability = $"{attribute.AuthorizationFeature}:create", AllowRemoteMutation = true
                })
                : InvocationPolicyCheckResult.Failure(resolved.Error!, resolved.StatusCode);
            Results.Add(result);
            if (result.IsSuccess) resolved.Context!.EffectiveTenantId.Should().Be(metadata.RequestedTenantId);
            return result;
        }

        public Task<ActorIdentityValidationResult> ValidateAsync(string token, CancellationToken ct = default)
        {
            var capabilities = new HashSet<string>();
            if (HasDelegation) capabilities.Add("identity.tenants:manage");
            if (HasRoleCreate) capabilities.Add("identity.roles:create");
            return Task.FromResult(ActorIdentityValidationResult.Success(new TrustedActorIdentity(
                Guid.NewGuid(), Guid.NewGuid(), ActorTenantId, Guid.NewGuid(), new HashSet<string>(), capabilities,
                "test-generation", DateTimeOffset.UtcNow.AddMinutes(10))));
        }

        public Task<ServiceIdentityValidationResult> ValidateAsync(string token, string audience, CancellationToken ct = default) =>
            Task.FromResult(ServiceIdentityValidationResult.Success(new TrustedServiceIdentity(
                XFrameworkServiceNames.Portal, audience,
                HasMutationScope ? new HashSet<string> { XFrameworkServiceScopes.DataContextMutate, XFrameworkServiceScopes.TenantTarget } : new HashSet<string>(),
                "test-generation")));
    }

    private sealed class RecordingNavigation : NavigationManager
    {
        public string? LastUri { get; private set; }
        public RecordingNavigation() => Initialize("http://localhost/", "http://localhost/identity/tenants");
        protected override void NavigateToCore(string uri, bool forceLoad) => LastUri = uri;
    }

    private sealed class NoBrowserJavaScript : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            throw new AssertionException("Static rendering must not invoke JavaScript");
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken ct, object?[]? args) => InvokeAsync<TValue>(identifier, args);
    }
}
