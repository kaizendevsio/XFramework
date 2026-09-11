using System.Reflection;
using System.Runtime.CompilerServices;
using BlazorBlueprint.Components;
using FluentAssertions;
using IdentityServer.Domain.Shared.Contracts;
using MemoryPack;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Wallets.Domain.Shared.Contracts;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.DataContext;
using XFramework.Integration.DataContext;
using XFramework.Integration.Extensions;
using XFramework.Portal.Features.Administration.Pages.Admin;

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
        SetProperty("RequestMetadata", _services.GetRequiredService<RequestMetadata>());
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
        SetProperty("RequestMetadata", new RequestMetadata());
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

    private sealed class RecordingWrapper : IDataContextServiceWrapper
    {
        public List<SaveChangesRequest> Batches { get; } = [];
        public DataContextResult? NextResult { get; set; }
        public WalletType? Wallet { get; set; }

        public Task<byte[]> ExecuteChangesAsync(byte[] bytes, CancellationToken ct = default)
        {
            Batches.Add(MemoryPackSerializer.Deserialize<SaveChangesRequest>(bytes)!);
            var result = NextResult ?? DataContextResult.Success();
            NextResult = null;
            return Task.FromResult(MemoryPackSerializer.Serialize(result));
        }

        public Task<byte[]> ExecuteQueryAsync(byte[] bytes, CancellationToken ct = default)
        {
            var query = MemoryPackSerializer.Deserialize<QueryDescriptor>(bytes)!;
            return Task.FromResult(query.EntityTypeName == nameof(WalletType)
                ? query.Mode == QueryExecutionMode.FirstOrDefault
                    ? MemoryPackSerializer.Serialize(Wallet)
                    : MemoryPackSerializer.Serialize(new List<WalletType>())
                : MemoryPackSerializer.Serialize(new List<IdentityRoleTypeGroup>()));
        }

        public async IAsyncEnumerable<byte[]> ExecuteQueryStreamAsync(byte[] bytes,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            await Task.CompletedTask;
            yield break;
        }
    }
}
