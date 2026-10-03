using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using BlazorBlueprint.Components;
using Bolt.Client;
using FluentAssertions;
using IdentityServer.Domain.Shared.Contracts;
using IdentityServer.Domain.Shared.Contracts.Requests;
using IdentityServer.Integration.Drivers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.DataContext;
using XFramework.Portal.Features.Identity.Pages;
using XFramework.Portal.Features.Identity.Services;

namespace XFramework.Core.Tests.Services.FeatureGates;

[TestFixture]
[Category("Area:PortalContract")]
public sealed class TenantDetailModuleFeatureTests
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private TenantDetail _page = null!;
    private Tenant _tenant = null!;
    private Guid _originalStamp;
    private Guid _refreshedStamp;
    private BoltClient _boltClient = null!;
    private ServiceProvider _services = null!;
    private ToastService _toasts = null!;
    private Mock<IIdentityServerServiceWrapper> _identity = null!;
    private Mock<ITenantModuleFeatureCatalog> _catalog = null!;
    private Mock<IRemoteQuery<Tenant>> _tenantQuery = null!;
    private Mock<IRemoteQuery<TenantModuleFeature>> _featureQuery = null!;
    private List<TenantModuleFeatureDefinition> _definitions = null!;
    private List<TenantModuleFeature> _storedRows = null!;
    private SetTenantModuleFeaturesRequest? _initialization;
    private HttpStatusCode _initializationStatus;
    private List<string> _operations = null!;

    [SetUp]
    public void SetUp()
    {
        _originalStamp = Guid.NewGuid();
        _refreshedStamp = Guid.NewGuid();
        _tenant = new Tenant { Id = Guid.NewGuid(), ConcurrencyStamp = _originalStamp };
        _page = new TenantDetail();
        typeof(TenantDetail).GetProperty(nameof(TenantDetail.Id))!.SetValue(_page, _tenant.Id);
        SetField("_tenant", _tenant);
        _initialization = null;
        _operations = [];
        _initializationStatus = HttpStatusCode.OK;
        _definitions =
        [
            new("identity", "", "Identity", "", "users"),
            new("identity", "credentials", "Credentials", "", "key"),
            new("wallets", "", "Wallets", "", "wallet", DefaultEnabled: false)
        ];
        var existingDisabled = Feature("identity", "", enabled: false);
        _storedRows = [existingDisabled];

        _featureQuery = Query<TenantModuleFeature>();
        _featureQuery.Setup(query => query.OrderBy(It.IsAny<Expression<Func<TenantModuleFeature, string>>>()))
            .Returns(_featureQuery.Object);
        _featureQuery.Setup(query => query.ThenBy(It.IsAny<Expression<Func<TenantModuleFeature, string>>>()))
            .Returns(_featureQuery.Object);
        var reads = 0;
        _featureQuery.Setup(query => query.ToListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                _operations.Add("features");
                return ++reads == 1 ? [existingDisabled] : _storedRows;
            });
        _tenantQuery = Query<Tenant>();
        _tenantQuery.Setup(query => query.FirstOrDefaultAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                _operations.Add("stamp");
                return new Tenant { Id = _tenant.Id, ConcurrencyStamp = _refreshedStamp };
            });
        var data = new Mock<IDataContext>(MockBehavior.Strict);
        data.Setup(context => context.Query<TenantModuleFeature>()).Returns(_featureQuery.Object);
        data.Setup(context => context.Query<Tenant>()).Returns(_tenantQuery.Object);
        SetProperty("DataContext", data.Object);

        _identity = new Mock<IIdentityServerServiceWrapper>(MockBehavior.Strict);
        _identity.Setup(wrapper => wrapper.SetTenantModuleFeatures(
                It.IsAny<SetTenantModuleFeaturesRequest>(), It.IsAny<CancellationToken>()))
            .Callback<SetTenantModuleFeaturesRequest, CancellationToken>((request, _) =>
            {
                _operations.Add("initialize");
                _initialization = request;
            })
            .ReturnsAsync(() => new CmdResponse { HttpStatusCode = _initializationStatus });
        SetProperty("IdentityServer", _identity.Object);

        _catalog = new Mock<ITenantModuleFeatureCatalog>(MockBehavior.Strict);
        _catalog.SetupGet(catalog => catalog.All).Returns(() => _definitions);
        _boltClient = new BoltClient(new Uri("ws://localhost/bolt"), "portal-test", "Portal test",
            new BoltClientOptions(), NullLogger<BoltClient>.Instance);
        SetProperty("ModuleFeatureDefinitionResolver", new TenantModuleFeatureDefinitionResolver(
            _catalog.Object, _boltClient, NullLogger<TenantModuleFeatureDefinitionResolver>.Instance));
        _services = new ServiceCollection().AddLogging().BuildServiceProvider();
        _toasts = ActivatorUtilities.CreateInstance<ToastService>(_services);
        SetProperty("ToastService", _toasts);
    }

    [TearDown]
    public async Task TearDown()
    {
        await _boltClient.DisposeAsync();
        _services.Dispose();
    }

    [Test]
    public async Task LoadModuleFeatures_SuccessfulInitialization_ReloadsRowsAndStampWithoutChangingExistingDisabledToggle()
    {
        _storedRows.Add(Feature("identity", "credentials", enabled: true));
        _storedRows.Add(Feature("wallets", "", enabled: false));

        await LoadModuleFeatures();

        _toasts.Toasts.Should().BeEmpty();
        AssertSingleInitializationAndReload();
        _initialization!.Features.Select(feature => (feature.ModuleKey, feature.SubFeatureKey, feature.IsEnabled))
            .Should().BeEquivalentTo(new[] { ("identity", "credentials", true), ("wallets", "", false) });
        _tenant.ConcurrencyStamp.Should().Be(_refreshedStamp);
        CheckedKeys().Should().BeEquivalentTo(["identity.credentials"]);
    }

    [Test]
    public async Task LoadModuleFeatures_CompetingCompleteInitializationConflict_UsesStoredValuesWithoutErrorOrRetry()
    {
        _initializationStatus = HttpStatusCode.Conflict;
        _storedRows.Add(Feature("IDENTITY", "CREDENTIALS", enabled: false));
        _storedRows.Add(Feature("wallets", "", enabled: true));

        await LoadModuleFeatures();

        _toasts.Toasts.Should().BeEmpty();
        AssertSingleInitializationAndReload();
        _tenant.ConcurrencyStamp.Should().Be(_refreshedStamp);
        CheckedKeys().Should().BeEquivalentTo(["wallets"], "persisted values override initialization defaults");
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task LoadModuleFeatures_IncompleteInitializationConflict_ReportsOriginalMissingKeys(bool definitionDisappears)
    {
        _initializationStatus = HttpStatusCode.Conflict;
        _storedRows.Add(Feature("identity", "credentials", enabled: false));
        if (definitionDisappears)
        {
            _catalog.SetupSequence(catalog => catalog.All)
                .Returns(_definitions)
                .Returns(_definitions.Where(definition => definition.ModuleKey != "wallets").ToList());
        }

        await LoadModuleFeatures();

        AssertInitializationError();
        AssertSingleInitializationAndReload();
        _tenant.ConcurrencyStamp.Should().Be(_refreshedStamp);
        CheckedKeys().Should().BeEmpty();
    }

    [TestCase(HttpStatusCode.Forbidden)]
    [TestCase(HttpStatusCode.InternalServerError)]
    public async Task LoadModuleFeatures_GenuineFailure_ReportsErrorEvenWhenReloadContainsAllKeys(HttpStatusCode status)
    {
        _initializationStatus = status;
        _storedRows.Add(Feature("identity", "credentials", enabled: false));
        _storedRows.Add(Feature("wallets", "", enabled: false));

        await LoadModuleFeatures();

        AssertInitializationError();
        AssertSingleInitializationAndReload();
        _tenant.ConcurrencyStamp.Should().Be(_originalStamp);
        _tenantQuery.Verify(query => query.FirstOrDefaultAsync(It.IsAny<CancellationToken>()), Times.Never);
        CheckedKeys().Should().BeEmpty();
    }

    [Test]
    public async Task LoadModuleFeatures_ConflictWithDeletedMissingRow_StillReportsInitializationError()
    {
        _initializationStatus = HttpStatusCode.Conflict;
        _storedRows.Add(Feature("identity", "credentials", enabled: false));
        var deleted = Feature("wallets", "", enabled: false);
        deleted.IsDeleted = true;
        _storedRows.Add(deleted);

        await LoadModuleFeatures();

        AssertInitializationError();
        AssertSingleInitializationAndReload();
    }

    private void AssertSingleInitializationAndReload()
    {
        _identity.Verify(wrapper => wrapper.SetTenantModuleFeatures(
            It.IsAny<SetTenantModuleFeaturesRequest>(), It.IsAny<CancellationToken>()), Times.Once);
        _initialization.Should().NotBeNull();
        _initialization!.TenantId.Should().Be(_tenant.Id);
        _initialization.ExpectedConcurrencyStamp.Should().Be(_originalStamp);
        _initialization.Features.Should().HaveCount(2).And.NotContain(feature =>
            feature.ModuleKey == "identity" && feature.SubFeatureKey == "");
        _featureQuery.Verify(query => query.ToListAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
        _featureQuery.Verify(query => query.NoCache(), Times.Exactly(2));
        _catalog.VerifyGet(catalog => catalog.All, Times.Exactly(2));
        _operations.Should().Equal(_initializationStatus is HttpStatusCode.OK or HttpStatusCode.Conflict
            ? ["features", "initialize", "stamp", "features"]
            : ["features", "initialize", "features"]);
        Field<bool>("_moduleFeaturesLoading").Should().BeFalse();
        var rows = Field<IEnumerable>("_moduleFeatureRows").Cast<object>();
        var identity = rows.Single(row => (string)row.GetType().GetProperty("Key")!.GetValue(row)! == "identity");
        identity.GetType().GetProperty("IsEnabled")!.GetValue(identity).Should().Be(false);
    }

    private void AssertInitializationError()
    {
        var toast = _toasts.Toasts.Should().ContainSingle().Subject;
        toast.Title.Should().Be("Initialization failed");
        toast.Description.Should().Be("Module toggles could not be initialized.");
    }

    private TenantModuleFeature Feature(string module, string subFeature, bool enabled) => new()
    {
        Id = Guid.NewGuid(), TenantId = _tenant.Id, ModuleKey = module, SubFeatureKey = subFeature,
        IsEnabled = enabled, ConcurrencyStamp = Guid.NewGuid()
    };

    private static Mock<IRemoteQuery<T>> Query<T>() where T : class
    {
        var query = new Mock<IRemoteQuery<T>>(MockBehavior.Strict);
        query.Setup(value => value.IgnoreQueryFilters()).Returns(query.Object);
        query.Setup(value => value.NoCache()).Returns(query.Object);
        query.Setup(value => value.Where(It.IsAny<Expression<Func<T, bool>>>())).Returns(query.Object);
        return query;
    }

    private HashSet<string> CheckedKeys() => Field<HashSet<string>>("_moduleFeatureCheckedKeys");
    private T Field<T>(string name) => (T)typeof(TenantDetail).GetField(name, PrivateInstance)!.GetValue(_page)!;
    private void SetField(string name, object value) =>
        typeof(TenantDetail).GetField(name, PrivateInstance)!.SetValue(_page, value);
    private void SetProperty(string name, object value) =>
        typeof(TenantDetail).GetProperty(name, PrivateInstance)!.SetValue(_page, value);
    private Task LoadModuleFeatures() =>
        (Task)typeof(TenantDetail).GetMethod("LoadModuleFeatures", PrivateInstance)!.Invoke(_page, null)!;
}
