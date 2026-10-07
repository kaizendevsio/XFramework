using System.Reflection;
using System.Security.Claims;
using System.Runtime.CompilerServices;
using BlazorBlueprint.Components;
using FluentAssertions;
using IdentityServer.Domain.Shared.Contracts;
using MemoryPack;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Logging.Abstractions;
using Wallets.Domain.Shared.Contracts;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.DataContext;
using XFramework.Integration.DataContext;
using XFramework.Integration.Extensions;
using XFramework.Portal.Features.Administration.Pages.Admin;
using XFramework.Portal.Shared;

namespace Portal.E2ETests;

[TestFixture, NonParallelizable]
[Category("Area:PortalContract")]
public sealed class ReferenceDataSaveTests
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private ServiceProvider _services = null!;
    private RecordingWrapper _wrapper = null!;
    private ReferenceData _page = null!;
    private object? _originalMap;
    private static readonly FieldInfo WrapperMap =
        typeof(RemoteDataContext).GetField("_wrapperMap", BindingFlags.Static | BindingFlags.NonPublic)!;

    [SetUp]
    public void SetUp()
    {
        // The host loads this integration assembly during startup; it owns WalletType's generated tracker.
        typeof(Wallets.Integration.Drivers.IWalletsServiceWrapper).Assembly
            .GetType("XFramework.Integration.DataContext.ChangeTrackerRegistry").Should().NotBeNull();
        _originalMap = WrapperMap.GetValue(null);
        WrapperMap.SetValue(null, new Dictionary<string, string>
        {
            [nameof(IdentityRoleTypeGroup)] = typeof(RecordingWrapper).FullName!,
            [nameof(WalletType)] = typeof(RecordingWrapper).FullName!
        });
        _wrapper = new RecordingWrapper();
        var services = new ServiceCollection().AddLogging().AddSingleton(_wrapper);
        services.AddTrustedInvocationSecurity();
        services.AddScoped(_ => new RequestMetadata { RequestedTenantId = _tenantId });
        // Match Portal Program.cs: the host uses the uncached registration, not
        // DataContext.RemoteDataContextExtensions (which adds optional client-cache services).
        XFramework.Integration.Extensions.ServiceCollectionExtensions.AddRemoteDataContext(services);
        _services = services.BuildServiceProvider();
        _page = new ReferenceData();
        SetProperty("Services", _services);
        _page.TenantId = _tenantId;
        SetProperty("AuthenticationStateTask", Task.FromResult(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(PortalAuthClaims.IsSuperUser, "true")], "test")))));
        SetProperty("DataContext", _services.GetRequiredService<IDataContext>());
        SetProperty("ToastService", ActivatorUtilities.CreateInstance<ToastService>(_services));
        SetProperty("Logger", NullLogger<ReferenceData>.Instance);
        SetField("_activeTab", "role-type-groups");
        SetField("_formName", "User");
    }

    [TearDown]
    public void TearDown()
    {
        WrapperMap.SetValue(null, _originalMap);
        _services.Dispose();
    }

    [Test]
    public async Task Create_UsesRequestedTenantAndGeneratesRequiredState()
    {
        await Invoke("SaveRecord");
        var request = _wrapper.Batches.Should().ContainSingle().Subject;
        request.Metadata!.RequestedTenantId.Should().Be(_tenantId);
        var entity = MemoryPackSerializer.Deserialize<IdentityRoleTypeGroup>(request.Changes.Single().SerializedEntity)!;
        entity.Name.Should().Be("User");
        entity.TenantId.Should().Be(_tenantId);
        entity.ConcurrencyStamp.Should().NotBeEmpty();
        entity.SystemReferenceId.Should().NotBeEmpty();
        entity.IsEnabled.Should().BeTrue();
    }

    [Test]
    public async Task FailedCreate_RetryDoesNotReplayOldBatch()
    {
        _wrapper.NextResult = DataContextResult.Failure("Denied", 403);
        await Invoke("SaveRecord");
        SetField("_formName", "Corrected");
        await Invoke("SaveRecord");
        _wrapper.Batches.Should().HaveCount(2).And.OnlyContain(batch => batch.Changes.Count == 1);
        var retried = MemoryPackSerializer.Deserialize<IdentityRoleTypeGroup>(_wrapper.Batches[1].Changes[0].SerializedEntity)!;
        retried.Name.Should().Be("Corrected");
    }

    [Test]
    public async Task FailedCreate_NextModuleSaveDoesNotReplayFailedChanges()
    {
        _wrapper.NextResult = DataContextResult.Failure("Denied", 403);
        await Invoke("SaveRecord");
        SetField("_activeTab", "wallet-types");
        SetField("_formName", "Wallet");
        SetField("_formCode", "TEST");
        await Invoke("SaveRecord");
        _wrapper.Batches.Should().HaveCount(2).And.OnlyContain(batch => batch.Changes.Count == 1);
        _wrapper.Batches[1].Changes[0].EntityTypeName.Should().Be(nameof(WalletType));
    }

    [Test]
    public async Task Delete_UsesOriginalTenantAndConcurrency()
    {
        var wallet = ExistingWallet();
        var remove = typeof(ReferenceData).GetMethod("RemoveEntity", BindingFlags.Instance | BindingFlags.NonPublic)!
            .MakeGenericMethod(typeof(WalletType));
        var result = await (Task<DataContextResult>)remove.Invoke(_page, new object[] { wallet })!;
        result.IsSuccess.Should().BeTrue();
        var batch = _wrapper.Batches.Should().ContainSingle().Subject;
        batch.Metadata!.RequestedTenantId.Should().Be(wallet.TenantId);
        batch.Changes[0].Operation.Should().Be(ChangeOperation.Remove);
        MemoryPackSerializer.Deserialize<WalletType>(batch.Changes[0].SerializedEntity)!
            .ConcurrencyStamp.Should().Be(wallet.ConcurrencyStamp);
    }

    [Test]
    public async Task Edit_UsesFieldPatchAndPreservesHiddenFieldsAndConcurrency()
    {
        var wallet = ExistingWallet();
        _wrapper.Wallet = wallet;
        OpenWalletEditor(wallet);
        SetField("_formName", "Renamed");
        await Invoke("SaveRecord");
        var change = _wrapper.Batches.Should().ContainSingle().Subject.Changes.Single();
        change.Operation.Should().Be(ChangeOperation.Update);
        var patch = MemoryPackSerializer.Deserialize<FieldPatch>(change.SerializedEntity)!;
        patch.ExpectedConcurrencyStamp.Should().Be(wallet.ConcurrencyStamp);
        patch.Changes.Keys.Should().Equal(nameof(WalletType.Name));
        MemoryPackSerializer.Deserialize<string>(patch.Changes[nameof(WalletType.Name)]).Should().Be("Renamed");
    }

    [Test]
    public async Task Edit_StaleRecordDoesNotSave()
    {
        var wallet = ExistingWallet();
        OpenWalletEditor(wallet);
        _wrapper.Wallet = MemoryPackSerializer.Deserialize<WalletType>(MemoryPackSerializer.Serialize(wallet))!;
        _wrapper.Wallet.ConcurrencyStamp = Guid.NewGuid();
        SetField("_formName", "Stale edit");
        await Invoke("SaveRecord");
        _wrapper.Batches.Should().BeEmpty();
    }

    [Test]
    public async Task Create_WithoutTenantDoesNotChooseAnArbitraryTenant()
    {
        _page.TenantId = Guid.Empty;
        await Invoke("SaveRecord");
        _wrapper.Batches.Should().BeEmpty();
    }

    [Test]
    public async Task GlobalRecord_CannotBeEditedOrDeleted()
    {
        var wallet = ExistingWallet();
        wallet.TenantId = Guid.Empty;
        OpenWalletEditor(wallet);
        await Invoke("SaveRecord");
        var remove = typeof(ReferenceData).GetMethod("RemoveEntity", BindingFlags.Instance | BindingFlags.NonPublic)!
            .MakeGenericMethod(typeof(WalletType));
        var result = await (Task<DataContextResult>)remove.Invoke(_page, new object[] { wallet })!;
        result.IsSuccess.Should().BeFalse();
        _wrapper.Batches.Should().BeEmpty();
    }

    [Test]
    public async Task RouteTenant_IgnoresMutableSharedRequestMetadata()
    {
        _services.GetRequiredService<RequestMetadata>().RequestedTenantId = Guid.NewGuid();
        await Invoke("SaveRecord");
        _wrapper.Batches.Should().ContainSingle().Which.Metadata!.RequestedTenantId.Should().Be(_tenantId);
        _wrapper.Queries.Should().OnlyContain(query => query.Metadata!.RequestedTenantId == _tenantId);
    }

    [Test]
    public async Task CrossTenantRecord_CannotBeEditedOrDeletedEvenBySuperUser()
    {
        var wallet = ExistingWallet();
        wallet.TenantId = Guid.NewGuid();
        OpenWalletEditor(wallet);
        await Invoke("SaveRecord");
        var remove = typeof(ReferenceData).GetMethod("RemoveEntity", BindingFlags.Instance | BindingFlags.NonPublic)!
            .MakeGenericMethod(typeof(WalletType));
        var result = await (Task<DataContextResult>)remove.Invoke(_page, new object[] { wallet })!;
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(403);
        _wrapper.Batches.Should().BeEmpty();
    }

    [Test]
    public async Task Create_PendingAuthorization_FreezesSubmittedFields()
    {
        var authorization = new TaskCompletionSource<AuthenticationState>(TaskCreationOptions.RunContinuationsAsynchronously);
        SetProperty("AuthenticationStateTask", authorization.Task);
        var pending = Invoke("SaveRecord");
        try
        {
            SetField("_formName", "Not submitted");
        }
        finally
        {
            authorization.SetResult(SuperUserState());
            await pending;
        }

        var entity = MemoryPackSerializer.Deserialize<IdentityRoleTypeGroup>(_wrapper.Batches.Single().Changes.Single().SerializedEntity)!;
        entity.Name.Should().Be("User");
    }

    [TestCase("cancel")]
    [TestCase("cancel-reopen")]
    [TestCase("replace")]
    [TestCase("route")]
    [TestCase("tab")]
    public async Task Edit_PendingRead_InvalidatedDraftDoesNotWrite(string change)
    {
        var wallet = ExistingWallet();
        _wrapper.Wallet = wallet;
        OpenWalletEditor(wallet);
        SetField("_formName", "Submitted");
        var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _wrapper.NextQueryGate = gate.Task;
        var pending = Invoke("SaveRecord");
        try
        {
            await _wrapper.QueryStarted.Task;
            switch (change)
            {
                case "cancel":
                    Call("OnDialogOpenChanged", false);
                    break;
                case "cancel-reopen":
                    Call("OnDialogOpenChanged", false);
                    OpenWalletEditor(ExistingWallet());
                    SetField("_formName", "Replacement");
                    break;
                case "replace":
                    OpenWalletEditor(ExistingWallet());
                    SetField("_formName", "Replacement");
                    break;
                case "route":
                    _page.TenantId = Guid.NewGuid();
                    await Invoke("OnParametersSetAsync");
                    OpenWalletEditor(ExistingWallet());
                    break;
                case "tab":
                    Field<HashSet<string>>("_loadedTabs").Add("role-type-groups");
                    await (Task)Call("OnReferenceCategoryChanged", "role-type-groups")!;
                    Call("OpenAddDialog");
                    break;
            }
        }
        finally
        {
            gate.SetResult(true);
            await pending;
        }

        _wrapper.Batches.Should().BeEmpty();
        if (change != "cancel") Field<bool>("_dialogOpen").Should().BeTrue();
        if (change is "replace" or "cancel-reopen") Field<string>("_formName").Should().Be("Replacement");
    }

    [Test]
    public async Task Edit_PendingRead_FreezesSubmittedFieldsWithoutDraftReplacement()
    {
        var wallet = ExistingWallet();
        _wrapper.Wallet = wallet;
        OpenWalletEditor(wallet);
        SetField("_formName", "Submitted");
        var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _wrapper.NextQueryGate = gate.Task;
        var pending = Invoke("SaveRecord");
        try
        {
            await _wrapper.QueryStarted.Task;
            SetField("_formName", "Not submitted");
        }
        finally
        {
            gate.SetResult(true);
            await pending;
        }

        var patch = MemoryPackSerializer.Deserialize<FieldPatch>(_wrapper.Batches.Single().Changes.Single().SerializedEntity)!;
        MemoryPackSerializer.Deserialize<string>(patch.Changes[nameof(WalletType.Name)]).Should().Be("Submitted");
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task Save_PendingWrite_DoesNotCloseReplacementDialogOrReloadTenant(bool changeTenant)
    {
        Call("OpenAddDialog");
        SetField("_formName", "Submitted");
        var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _wrapper.NextWriteGate = gate.Task;
        var pending = Invoke("SaveRecord");
        try
        {
            await _wrapper.WriteStarted.Task;
            if (changeTenant) _page.TenantId = Guid.NewGuid();
            Call("OpenAddDialog");
            SetField("_formName", "Replacement");
            SetField("_saving", true);
        }
        finally
        {
            gate.SetResult(true);
            await pending;
        }

        _wrapper.Batches.Should().ContainSingle().Which.Metadata!.RequestedTenantId.Should().Be(_tenantId);
        _wrapper.Queries.Should().BeEmpty("a stale completion must not reload the replacement route or tab");
        Field<bool>("_dialogOpen").Should().BeTrue();
        Field<string>("_formName").Should().Be("Replacement");
        Field<bool>("_saving").Should().BeTrue("the replacement draft owns its own saving state");
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task Delete_PendingWrite_DoesNotCloseReplacementConfirmationOrReloadTenant(bool changeTenant)
    {
        Call("OpenDeleteDialog", ExistingWallet(), "Original");
        var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _wrapper.NextWriteGate = gate.Task;
        var pending = Invoke("ConfirmDelete");
        var replacement = ExistingWallet();
        try
        {
            await _wrapper.WriteStarted.Task;
            if (changeTenant) _page.TenantId = replacement.TenantId = Guid.NewGuid();
            Call("OpenDeleteDialog", replacement, "Replacement");
        }
        finally
        {
            gate.SetResult(true);
            await pending;
        }

        _wrapper.Batches.Should().ContainSingle().Which.Metadata!.RequestedTenantId.Should().Be(_tenantId);
        _wrapper.Queries.Should().BeEmpty();
        Field<bool>("_deleteDialogOpen").Should().BeTrue();
        Field<object>("_deleteTarget").Should().BeSameAs(replacement);
        Field<string>("_deleteName").Should().Be("Replacement");
    }

    [Test]
    public async Task Delete_PendingAuthorization_CancelledDraftDoesNotWrite()
    {
        Call("OpenDeleteDialog", ExistingWallet(), "Original");
        var authorization = new TaskCompletionSource<AuthenticationState>(TaskCreationOptions.RunContinuationsAsynchronously);
        SetProperty("AuthenticationStateTask", authorization.Task);
        var pending = Invoke("ConfirmDelete");
        try
        {
            Call("OnDeleteDialogOpenChanged", false);
            Call("OpenDeleteDialog", ExistingWallet(), "Replacement");
        }
        finally
        {
            authorization.SetResult(SuperUserState());
            await pending;
        }

        _wrapper.Batches.Should().BeEmpty();
        Field<bool>("_deleteDialogOpen").Should().BeTrue();
        Field<string>("_deleteName").Should().Be("Replacement");
    }

    [Test]
    public async Task Create_PendingAuthorization_RouteChangeDoesNotWriteOrRevokeReplacementAccess()
    {
        Call("OpenAddDialog");
        SetField("_formName", "Submitted");
        var authorization = new TaskCompletionSource<AuthenticationState>(TaskCreationOptions.RunContinuationsAsynchronously);
        SetProperty("AuthenticationStateTask", authorization.Task);
        var pending = Invoke("SaveRecord");
        try
        {
            _page.TenantId = Guid.NewGuid();
            SetProperty("AuthenticationStateTask", Task.FromResult(SuperUserState()));
            await Invoke("OnParametersSetAsync");
            Call("OpenAddDialog");
            SetField("_formName", "Replacement");
        }
        finally
        {
            authorization.SetResult(SuperUserState());
            await pending;
        }

        _wrapper.Batches.Should().BeEmpty();
        Field<bool>("_canManageReferenceData").Should().BeTrue();
        Field<bool>("_dialogOpen").Should().BeTrue();
        Field<string>("_formName").Should().Be("Replacement");
        _wrapper.Queries.Should().ContainSingle().Which.Metadata!.RequestedTenantId.Should().Be(_page.TenantId);
    }

    [Test]
    public async Task Reload_PendingRead_RouteChangeDoesNotOverwriteReplacementList()
    {
        SetField("_canManageReferenceData", true);
        var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _wrapper.NextQueryGate = gate.Task;
        var pending = Invoke("ReloadActiveTab");
        var replacement = new IdentityRoleTypeGroup { Id = Guid.NewGuid(), TenantId = Guid.NewGuid(), Name = "Replacement" };
        try
        {
            await _wrapper.QueryStarted.Task;
            _page.TenantId = replacement.TenantId;
            await Invoke("OnParametersSetAsync");
            Field<List<IdentityRoleTypeGroup>>("_roleTypeGroups").Add(replacement);
        }
        finally
        {
            gate.SetResult(true);
            await pending;
        }

        Field<List<IdentityRoleTypeGroup>>("_roleTypeGroups").Should().ContainSingle().Which.Should().BeSameAs(replacement);
    }

    private static AuthenticationState SuperUserState() => new(new ClaimsPrincipal(new ClaimsIdentity(
        [new Claim(PortalAuthClaims.IsSuperUser, "true")], "test")));

    private WalletType ExistingWallet() => new()
    {
        Id = Guid.NewGuid(), TenantId = _tenantId, Name = "Original", Code = "CODE", Type = 7,
        CurrencyTypeId = Guid.NewGuid(), SystemReferenceId = Guid.NewGuid(),
        ConcurrencyStamp = Guid.NewGuid(), CreatedAt = DateTime.UtcNow, IsEnabled = true
    };

    private void OpenWalletEditor(WalletType wallet)
    {
        SetField("_activeTab", "wallet-types");
        typeof(ReferenceData).GetMethod("OpenEditWalletType", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(_page, new object[] { wallet });
    }

    private void SetProperty(string name, object value) =>
        typeof(ReferenceData).GetProperty(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(_page, value);

    private void SetField(string name, object value) =>
        typeof(ReferenceData).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(_page, value);

    private Task Invoke(string name) =>
        (Task)typeof(ReferenceData).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(_page, null)!;

    private object? Call(string name, params object[] args) =>
        typeof(ReferenceData).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(_page, args);

    private T Field<T>(string name) =>
        (T)typeof(ReferenceData).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(_page)!;

    private sealed class RecordingWrapper : IDataContextServiceWrapper
    {
        public List<SaveChangesRequest> Batches { get; } = [];
        public List<QueryDescriptor> Queries { get; } = [];
        public DataContextResult? NextResult { get; set; }
        public WalletType? Wallet { get; set; }
        public Task? NextWriteGate { get; set; }
        public Task? NextQueryGate { get; set; }
        public TaskCompletionSource<bool> WriteStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> QueryStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<byte[]> ExecuteChangesAsync(byte[] bytes, CancellationToken ct = default)
        {
            Batches.Add(MemoryPackSerializer.Deserialize<SaveChangesRequest>(bytes)!);
            var result = NextResult ?? DataContextResult.Success();
            NextResult = null;
            var gate = NextWriteGate;
            NextWriteGate = null;
            WriteStarted.TrySetResult(true);
            if (gate is not null) await gate;
            return MemoryPackSerializer.Serialize(result);
        }

        public async Task<byte[]> ExecuteQueryAsync(byte[] bytes, CancellationToken ct = default)
        {
            var query = MemoryPackSerializer.Deserialize<QueryDescriptor>(bytes)!;
            Queries.Add(query);
            var result = query.EntityTypeName == nameof(WalletType)
                ? query.Mode == QueryExecutionMode.FirstOrDefault
                    ? MemoryPackSerializer.Serialize(Wallet)
                    : MemoryPackSerializer.Serialize(new List<WalletType>())
                : MemoryPackSerializer.Serialize(new List<IdentityRoleTypeGroup>());
            var gate = NextQueryGate;
            NextQueryGate = null;
            QueryStarted.TrySetResult(true);
            if (gate is not null) await gate;
            return result;
        }

        public async IAsyncEnumerable<byte[]> ExecuteQueryStreamAsync(byte[] bytes,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            await Task.CompletedTask;
            yield break;
        }
    }
}
