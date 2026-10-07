using System.Net;
using System.Reflection;
using System.Security.Claims;
using BlazorBlueprint.Components;
using FluentAssertions;
using IdentityServer.Domain.Shared.Contracts;
using IdentityServer.Domain.Shared.Contracts.Requests;
using IdentityServer.Domain.Shared.Contracts.Responses;
using IdentityServer.Integration.Drivers;
using MemoryPack;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Integration.DataContext;
using XFramework.Portal.Features.Identity.Pages;
using XFramework.Portal.Shared;

namespace Portal.E2ETests;

[TestFixture, NonParallelizable]
[Category("Area:PortalContract")]
public sealed class RoleTypeDetailSaveRaceTests
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly FieldInfo WrapperMap = typeof(RemoteDataContext)
        .GetField("_wrapperMap", BindingFlags.Static | BindingFlags.NonPublic)!;
    private object? _originalMap;
    private ServiceProvider _services = null!;
    private RoleTypeDetail _page = null!;
    private readonly Dictionary<Guid, RoleState> _roles = [];
    private readonly List<UpdateTenantAuthorizationPolicyRequest> _policyWrites = [];
    private readonly List<SetRoleTypePermissionsRequest> _permissionWrites = [];
    private readonly List<GetRoleTypePermissionsRequest> _permissionReads = [];
    private Func<GetTenantAuthorizationPolicyRequest, Task<QueryResponse<TenantAuthorizationPolicyResponse>>> _readPolicy = null!;
    private Func<GetRoleTypePermissionsRequest, Task<QueryResponse<RoleTypePermissionsResponse>>> _readPermissions = null!;
    private Func<UpdateTenantAuthorizationPolicyRequest, Task<QueryResponse<TenantAuthorizationPolicyResponse>>> _writePolicy = null!;
    private Func<SetRoleTypePermissionsRequest, Task<QueryResponse<RoleTypePermissionsResponse>>> _writePermissions = null!;

    [SetUp]
    public void SetUp()
    {
        _originalMap = WrapperMap.GetValue(null);
        WrapperMap.SetValue(null, new Dictionary<string, string>
        {
            [nameof(IdentityRoleType)] = typeof(IIdentityServerServiceWrapper).FullName!
        });
        _roles.Clear(); _policyWrites.Clear(); _permissionWrites.Clear(); _permissionReads.Clear();
        _readPolicy = request => Task.FromResult(PolicyResponse(request.TenantId,
            _roles.Values.First(role => role.Role.TenantId == request.TenantId).PolicyStamp));
        _readPermissions = request => Task.FromResult(PermissionsResponse(_roles[request.RoleTypeId]));
        _writePolicy = request => Task.FromResult(PolicyResponse(request.TenantId, Guid.NewGuid()));
        _writePermissions = request => Task.FromResult(Success(new RoleTypePermissionsResponse
        {
            TenantId = request.Metadata!.RequestedTenantId!.Value,
            RoleTypeId = request.RoleTypeId, ConcurrencyStamp = Guid.NewGuid(), Permissions = request.Permissions
        }));
        var wrapper = new Mock<IIdentityServerServiceWrapper>(MockBehavior.Strict);
        wrapper.Setup(x => x.ExecuteQueryAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .Returns((byte[] bytes, CancellationToken _) =>
            {
                var query = MemoryPackSerializer.Deserialize<XFramework.Domain.Shared.DataContext.QueryDescriptor>(bytes)!;
                var id = (Guid)query.Filters.Single(filter => filter.PropertyName == nameof(IdentityRoleType.Id)).Value!;
                var tenantId = (Guid)query.Filters.Single(filter => filter.PropertyName == nameof(IdentityRoleType.TenantId)).Value!;
                var role = _roles[id].Role;
                role.TenantId.Should().Be(tenantId);
                return Task.FromResult(MemoryPackSerializer.Serialize(role));
            });
        wrapper.Setup(x => x.GetTenantAuthorizationPolicy(It.IsAny<GetTenantAuthorizationPolicyRequest>(), It.IsAny<CancellationToken>()))
            .Returns((GetTenantAuthorizationPolicyRequest request, CancellationToken _) => _readPolicy(request));
        wrapper.Setup(x => x.GetRoleTypePermissions(It.IsAny<GetRoleTypePermissionsRequest>(), It.IsAny<CancellationToken>()))
            .Returns((GetRoleTypePermissionsRequest request, CancellationToken _) =>
            {
                _permissionReads.Add(request);
                return _readPermissions(request);
            });
        wrapper.Setup(x => x.UpdateTenantAuthorizationPolicy(It.IsAny<UpdateTenantAuthorizationPolicyRequest>(), It.IsAny<CancellationToken>()))
            .Returns((UpdateTenantAuthorizationPolicyRequest request, CancellationToken _) =>
            {
                _policyWrites.Add(request);
                return _writePolicy(request);
            });
        wrapper.Setup(x => x.SetRoleTypePermissions(It.IsAny<SetRoleTypePermissionsRequest>(), It.IsAny<CancellationToken>()))
            .Returns((SetRoleTypePermissionsRequest request, CancellationToken _) =>
            {
                _permissionWrites.Add(request);
                return _writePermissions(request);
            });
        var services = new ServiceCollection().AddLogging().AddSingleton(wrapper.Object);
        services.AddBlazorBlueprintComponents();
        _services = services.BuildServiceProvider();
        _page = new RoleTypeDetail();
        SetProperty("DataContext", new RemoteDataContext(_services));
        SetProperty("IdentityServer", wrapper.Object);
        SetProperty("ToastService", _services.GetRequiredService<ToastService>());
        SetProperty("AuthenticationStateTask", Task.FromResult(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(PortalAuthClaims.RoleTypeId, PortalAccess.AdminRoleTypeId.ToString())], "test")))));
    }

    [TearDown]
    public async Task TearDown()
    {
        _page.Dispose();
        WrapperMap.SetValue(null, _originalMap);
        await _services.DisposeAsync();
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task Save_PolicyDelayedThenRouteChanged_DoesNotWriteNewRoleOrAlterItsPendingSave(bool otherTenant)
    {
        var first = NewRole();
        var next = NewRole(otherTenant ? Guid.NewGuid() : first.Role.TenantId);
        await NavigateAsync(first);
        EditPermission("allow");
        var firstGate = Gate<TenantAuthorizationPolicyResponse>();
        var nextGate = Gate<TenantAuthorizationPolicyResponse>();
        _writePolicy = _ => _policyWrites.Count == 1 ? firstGate.Task : nextGate.Task;
        var pendingFirst = InvokeAsync("SaveRoleTypePermissions");
        Task? pendingNext = null;
        try
        {
            await NavigateAsync(next);
            EditPermission("deny");
            pendingNext = InvokeAsync("SaveRoleTypePermissions");
            firstGate.SetResult(PolicyResponse(first.Role.TenantId, Guid.NewGuid()));
            await pendingFirst;

            _permissionWrites.Should().BeEmpty();
            Field<bool>("_saving").Should().BeTrue("the old save must not unlock the new save");
            Field<Guid>("_policyConcurrencyStamp").Should().Be(next.PolicyStamp);
            Field<Guid>("_roleTypeConcurrencyStamp").Should().Be(next.RoleStamp);
            PermissionValue().Should().Be("deny");
        }
        finally
        {
            firstGate.TrySetResult(PolicyResponse(first.Role.TenantId, Guid.NewGuid()));
            nextGate.TrySetResult(PolicyResponse(next.Role.TenantId, Guid.NewGuid()));
            await pendingFirst;
            if (pendingNext is not null) await pendingNext;
        }

        var write = _permissionWrites.Should().ContainSingle().Subject;
        write.RoleTypeId.Should().Be(next.Role.Id);
        write.Metadata!.RequestedTenantId.Should().Be(next.Role.TenantId);
        write.ExpectedConcurrencyStamp.Should().Be(next.RoleStamp);
        write.Permissions.Should().ContainSingle().Which.Effect.Should().Be(RoleCapabilityPermissionEffect.Deny);
        _policyWrites[0].TenantId.Should().Be(first.Role.TenantId);
        _policyWrites[0].ExpectedConcurrencyStamp.Should().Be(first.PolicyStamp);
        _policyWrites[0].Metadata!.RequestedTenantId.Should().Be(first.Role.TenantId);
    }

    [Test]
    public async Task Save_PermissionsDelayedThenRouteChanged_DoesNotOverwriteNewDraftStampsOrSavingState()
    {
        var first = NewRole();
        var next = NewRole();
        await NavigateAsync(first);
        EditPermission("allow");
        var firstGate = Gate<RoleTypePermissionsResponse>();
        _writePermissions = request => request.RoleTypeId == first.Role.Id
            ? firstGate.Task : Task.FromResult(PermissionsResponse(next, Guid.NewGuid()));
        var pendingFirst = InvokeAsync("SaveRoleTypePermissions");
        var nextGate = Gate<TenantAuthorizationPolicyResponse>();
        Task? pendingNext = null;
        try
        {
            await NavigateAsync(next);
            EditPermission("deny");
            _writePolicy = _ => nextGate.Task;
            pendingNext = InvokeAsync("SaveRoleTypePermissions");
            firstGate.SetResult(PermissionsResponse(first, Guid.NewGuid()));
            await pendingFirst;

            Field<Guid>("_roleTypeConcurrencyStamp").Should().Be(next.RoleStamp);
            Field<Guid>("_policyConcurrencyStamp").Should().Be(next.PolicyStamp);
            Field<bool>("_saving").Should().BeTrue();
            PermissionValue().Should().Be("deny");
            _permissionWrites.Should().ContainSingle().Which.RoleTypeId.Should().Be(first.Role.Id);
        }
        finally
        {
            firstGate.TrySetResult(PermissionsResponse(first));
            nextGate.TrySetResult(PolicyResponse(next.Role.TenantId, Guid.NewGuid()));
            await pendingFirst;
            if (pendingNext is not null) await pendingNext;
        }
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task Load_DelayedResponseThenRouteChanged_DoesNotReplaceNewRoleDraft(bool policyDelayed)
    {
        var first = NewRole();
        var next = NewRole();
        var policyGate = Gate<TenantAuthorizationPolicyResponse>();
        var permissionGate = Gate<RoleTypePermissionsResponse>();
        _readPolicy = request => request.TenantId == first.Role.TenantId && policyDelayed
            ? policyGate.Task : Task.FromResult(PolicyResponse(request.TenantId, next.PolicyStamp));
        _readPermissions = request => request.RoleTypeId == first.Role.Id
            ? permissionGate.Task : Task.FromResult(PermissionsResponse(next));
        var pendingFirst = NavigateAsync(first);
        try
        {
            await NavigateAsync(next);
            EditPermission("deny");
            SetDefault("allow");
            policyGate.TrySetResult(PolicyResponse(first.Role.TenantId, first.PolicyStamp));
            permissionGate.TrySetResult(PermissionsResponse(first));
            await pendingFirst;

            Field<IdentityRoleType>("_roleType").Id.Should().Be(next.Role.Id);
            Field<Guid>("_policyConcurrencyStamp").Should().Be(next.PolicyStamp);
            Field<Guid>("_roleTypeConcurrencyStamp").Should().Be(next.RoleStamp);
            Field<string>("_missingPermissionBehaviorValue").Should().Be("allow");
            PermissionValue().Should().Be("deny");
            Field<bool>("_permissionsLoaded").Should().BeTrue();
            Field<bool>("_loading").Should().BeFalse();
            _permissionReads.Count(request => request.RoleTypeId == next.Role.Id).Should().Be(1);
            if (policyDelayed) _permissionReads.Should().NotContain(request => request.RoleTypeId == first.Role.Id);
        }
        finally
        {
            policyGate.TrySetResult(PolicyResponse(first.Role.TenantId, first.PolicyStamp));
            permissionGate.TrySetResult(PermissionsResponse(first));
            await pendingFirst;
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task Save_DraftChangedWhilePolicyPending_SkipsPermissionsAndRetainsPolicyStampForRetry(bool changeDefault)
    {
        var role = NewRole();
        await NavigateAsync(role);
        EditPermission("allow");
        var gate = Gate<TenantAuthorizationPolicyResponse>();
        var policyStamp = Guid.NewGuid();
        _writePolicy = _ => gate.Task;
        var pending = InvokeAsync("SaveRoleTypePermissions");
        try
        {
            await InvokeAsync("SaveRoleTypePermissions");
            _policyWrites.Should().ContainSingle("a double click cannot start another save");
            if (changeDefault) SetDefault("allow");
            else EditPermission("deny");
        }
        finally
        {
            gate.TrySetResult(PolicyResponse(role.Role.TenantId, policyStamp));
            await pending;
        }

        _permissionWrites.Should().BeEmpty();
        Field<Guid>("_policyConcurrencyStamp").Should().Be(policyStamp);
        Field<bool>("_saving").Should().BeFalse();
        await InvokeAsync("SaveRoleTypePermissions");
        _policyWrites.Last().ExpectedConcurrencyStamp.Should().Be(policyStamp);
        _permissionWrites.Should().ContainSingle().Which.Permissions.Single().Effect.Should().Be(
            changeDefault ? RoleCapabilityPermissionEffect.Allow : RoleCapabilityPermissionEffect.Deny);
        _policyWrites.Last().MissingPermissionBehavior.Should().Be(
            changeDefault ? MissingPermissionBehavior.Allow : MissingPermissionBehavior.Deny);
    }

    [Test]
    public async Task Save_DraftChangedWhilePermissionsPending_PreservesNewDraftAndAdvancesSavedStamp()
    {
        var role = NewRole();
        await NavigateAsync(role);
        EditPermission("allow");
        var gate = Gate<RoleTypePermissionsResponse>();
        var roleStamp = Guid.NewGuid();
        _writePermissions = _ => gate.Task;
        var pending = InvokeAsync("SaveRoleTypePermissions");
        try { EditPermission("deny"); }
        finally
        {
            gate.TrySetResult(PermissionsResponse(role, roleStamp));
            await pending;
        }

        PermissionValue().Should().Be("deny");
        Field<Guid>("_roleTypeConcurrencyStamp").Should().Be(roleStamp);
        var original = _permissionWrites.Should().ContainSingle().Subject;
        original.RoleTypeId.Should().Be(role.Role.Id);
        original.ExpectedConcurrencyStamp.Should().Be(role.RoleStamp);
        original.Metadata!.RequestedTenantId.Should().Be(role.Role.TenantId);
        original.Permissions.Should().ContainSingle().Which.Effect.Should().Be(RoleCapabilityPermissionEffect.Allow);
        await InvokeAsync("SaveRoleTypePermissions");
        _permissionWrites.Last().ExpectedConcurrencyStamp.Should().Be(roleStamp);
        _permissionWrites.Last().Permissions.Single().Effect.Should().Be(RoleCapabilityPermissionEffect.Deny);
    }

    [TestCase(HttpStatusCode.Conflict)]
    [TestCase(HttpStatusCode.Forbidden)]
    public async Task Save_PermissionsRejected_RetainsDraftAndUpdatedPolicyStampForRetry(HttpStatusCode status)
    {
        var role = NewRole();
        await NavigateAsync(role);
        EditPermission("deny");
        var policyStamp = Guid.NewGuid();
        _writePolicy = _ => Task.FromResult(PolicyResponse(role.Role.TenantId, policyStamp));
        _writePermissions = _ => Task.FromResult(new QueryResponse<RoleTypePermissionsResponse> { HttpStatusCode = status });

        await InvokeAsync("SaveRoleTypePermissions");

        PermissionValue().Should().Be("deny");
        Field<Guid>("_policyConcurrencyStamp").Should().Be(policyStamp);
        Field<Guid>("_roleTypeConcurrencyStamp").Should().Be(role.RoleStamp);
        Field<bool>("_saving").Should().BeFalse();
        await InvokeAsync("SaveRoleTypePermissions");
        _policyWrites.Last().ExpectedConcurrencyStamp.Should().Be(policyStamp);
        _permissionWrites.Last().ExpectedConcurrencyStamp.Should().Be(role.RoleStamp);
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task Save_PendingThenDisposed_DoesNotContinueWritesOrShowToast(bool permissionsStage)
    {
        var role = NewRole();
        await NavigateAsync(role);
        var policyGate = Gate<TenantAuthorizationPolicyResponse>();
        var permissionsGate = Gate<RoleTypePermissionsResponse>();
        if (permissionsStage) _writePermissions = _ => permissionsGate.Task;
        else _writePolicy = _ => policyGate.Task;
        var pending = InvokeAsync("SaveRoleTypePermissions");
        _page.Dispose();
        policyGate.SetResult(PolicyResponse(role.Role.TenantId, Guid.NewGuid()));
        permissionsGate.SetResult(PermissionsResponse(role, Guid.NewGuid()));
        await pending;
        _permissionWrites.Should().HaveCount(permissionsStage ? 1 : 0);
        Field<Guid>("_roleTypeConcurrencyStamp").Should().Be(role.RoleStamp);
        _services.GetRequiredService<ToastService>().Toasts.Should().BeEmpty();
    }

    private RoleState NewRole(Guid? tenantId = null)
    {
        var tenant = tenantId ?? Guid.NewGuid();
        var role = new IdentityRoleType { Id = Guid.NewGuid(), TenantId = tenant, Name = "Role", IsEnabled = true };
        var state = new RoleState(role, _roles.Values.FirstOrDefault(item => item.Role.TenantId == tenant)?.PolicyStamp
            ?? Guid.NewGuid(), Guid.NewGuid());
        _roles.Add(role.Id, state);
        return state;
    }

    private Task NavigateAsync(RoleState role)
    {
        _page.TenantId = role.Role.TenantId;
        _page.RoleTypeId = role.Role.Id;
        return InvokeAsync("OnParametersSetAsync");
    }

    private void EditPermission(string value) => Invoke("SetPermissionValue",
        TenantModuleFeatureKeys.Find(TenantModuleFeatureKeys.Identity)!, "view", value);
    private void SetDefault(string value) => Invoke("SetMissingPermissionBehavior", value);
    private string PermissionValue() => Field<Dictionary<string, string>>("_permissionValues")["identity||view"];
    private object? Invoke(string method, params object[] arguments) =>
        typeof(RoleTypeDetail).GetMethod(method, PrivateInstance)!.Invoke(_page, arguments);
    private Task InvokeAsync(string method) => ((Task)Invoke(method)!).WaitAsync(TimeSpan.FromSeconds(10));
    private void SetProperty(string name, object value) => typeof(RoleTypeDetail).GetProperty(name, PrivateInstance)!.SetValue(_page, value);
    private T Field<T>(string name) => (T)typeof(RoleTypeDetail).GetField(name, PrivateInstance)!.GetValue(_page)!;
    private static TaskCompletionSource<QueryResponse<T>> Gate<T>() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    private static QueryResponse<T> Success<T>(T response) => new() { HttpStatusCode = HttpStatusCode.OK, Response = response };
    private static QueryResponse<TenantAuthorizationPolicyResponse> PolicyResponse(Guid tenantId, Guid stamp) =>
        Success(new TenantAuthorizationPolicyResponse { TenantId = tenantId, ConcurrencyStamp = stamp });
    private static QueryResponse<RoleTypePermissionsResponse> PermissionsResponse(RoleState role, Guid? stamp = null) =>
        Success(new RoleTypePermissionsResponse { TenantId = role.Role.TenantId, RoleTypeId = role.Role.Id,
            ConcurrencyStamp = stamp ?? role.RoleStamp });
    private sealed record RoleState(IdentityRoleType Role, Guid PolicyStamp, Guid RoleStamp);
}
