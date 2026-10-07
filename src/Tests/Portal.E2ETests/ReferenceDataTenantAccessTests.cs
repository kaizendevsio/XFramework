using System.Net;
using System.Reflection;
using System.Security.Claims;
using FluentAssertions;
using IdentityServer.Domain.Shared.Contracts;
using IdentityServer.Domain.Shared.Contracts.Requests;
using IdentityServer.Domain.Shared.Contracts.Responses;
using IdentityServer.Integration.Drivers;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.DataContext;
using XFramework.Portal.Features.Administration;
using XFramework.Portal.Features.Administration.Pages.Admin;
using XFramework.Portal.Shared;

namespace Portal.E2ETests;

[TestFixture]
[Category("Area:PortalContract")]
public sealed class ReferenceDataTenantAccessTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CredentialId = Guid.NewGuid();
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    [TestCase(false)]
    [TestCase(true)]
    public async Task TenantAdministrator_UsesServerCapabilityDecision(bool allowed)
    {
        var wrapper = CapabilityWrapper(allowed);
        var user = Principal();
        var result = await TenantReferenceDataAccess.CanManageAsync(user, TenantId, wrapper.Object);
        result.Should().Be(allowed);
        wrapper.Verify(x => x.CheckCredentialCapability(It.Is<CheckCredentialCapabilityRequest>(request =>
            request.CredentialId == CredentialId && request.ModuleKey == "identity"
            && request.SubFeatureKey == "tenants" && request.CapabilityKey == "manage"
            && request.Metadata.RequestedTenantId == TenantId), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task SuperUser_CanManageExplicitTenantWithoutBootstrapRoleId()
    {
        var wrapper = new Mock<IIdentityServerServiceWrapper>(MockBehavior.Strict);
        var user = Principal(superUser: true, actorTenantId: Guid.NewGuid());
        (await TenantReferenceDataAccess.CanManageAsync(user, TenantId, wrapper.Object)).Should().BeTrue();
        wrapper.VerifyNoOtherCalls();
    }

    [Test]
    public async Task TenantAdministrator_CannotUseAnotherTenantsRoute()
    {
        var wrapper = new Mock<IIdentityServerServiceWrapper>(MockBehavior.Strict);
        (await TenantReferenceDataAccess.CanManageAsync(Principal(), Guid.NewGuid(), wrapper.Object)).Should().BeFalse();
        wrapper.VerifyNoOtherCalls();
    }

    [Test]
    public async Task AnonymousOrMissingTenant_CannotUseSettings()
    {
        var wrapper = new Mock<IIdentityServerServiceWrapper>(MockBehavior.Strict);
        (await TenantReferenceDataAccess.CanManageAsync(new ClaimsPrincipal(), TenantId, wrapper.Object)).Should().BeFalse();
        (await TenantReferenceDataAccess.CanManageAsync(null, TenantId, wrapper.Object)).Should().BeFalse();
        (await TenantReferenceDataAccess.CanManageAsync(Principal(superUser: true), Guid.Empty, wrapper.Object)).Should().BeFalse();
        wrapper.VerifyNoOtherCalls();
    }

    [Test]
    public async Task CapabilityResponse_DifferentTenantOrCredential_IsDenied()
    {
        foreach (var response in new[]
        {
            new CredentialCapabilityCheckResponse { TenantId = Guid.NewGuid(), CredentialId = CredentialId, IsAllowed = true },
            new CredentialCapabilityCheckResponse { TenantId = TenantId, CredentialId = Guid.NewGuid(), IsAllowed = true }
        })
        {
            var wrapper = new Mock<IIdentityServerServiceWrapper>();
            wrapper.Setup(x => x.CheckCredentialCapability(It.IsAny<CheckCredentialCapabilityRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new QueryResponse<CredentialCapabilityCheckResponse> { HttpStatusCode = HttpStatusCode.OK, Response = response });
            (await TenantReferenceDataAccess.CanManageAsync(Principal(), TenantId, wrapper.Object)).Should().BeFalse();
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task DirectRoute_NormalUserOrPermissionFailure_DoesNotReadReferenceData(bool lookupFails)
    {
        var wrapper = CapabilityWrapper(false);
        if (lookupFails)
            wrapper.Setup(x => x.CheckCredentialCapability(It.IsAny<CheckCredentialCapabilityRequest>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Unavailable"));
        var page = Page(Principal(), wrapper.Object);
        await Invoke(page, "OnParametersSetAsync");
        typeof(ReferenceData).GetField("_canManageReferenceData", PrivateInstance)!.GetValue(page).Should().Be(false);
        typeof(ReferenceData).GetField("_loadedTenantId", PrivateInstance)!.GetValue(page).Should().BeNull();
        var load = typeof(ReferenceData).GetMethod("LoadList", PrivateInstance)!.MakeGenericMethod(typeof(IdentityRoleType));
        (await (Task<List<IdentityRoleType>>)load.Invoke(page, [100])!).Should().BeEmpty();
    }

    [Test]
    public async Task Mutation_RevokedTenantAdminPermission_IsRecheckedAndDenied()
    {
        var wrapper = CapabilityWrapper(false);
        var page = Page(Principal(), wrapper.Object);
        typeof(ReferenceData).GetField("_canManageReferenceData", PrivateInstance)!.SetValue(page, true);
        var remove = typeof(ReferenceData).GetMethod("RemoveEntity", PrivateInstance)!
            .MakeGenericMethod(typeof(IdentityRoleType));
        var result = await (Task<DataContextResult>)remove.Invoke(page,
            [new IdentityRoleType { Id = Guid.NewGuid(), TenantId = TenantId }])!;
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(403);
        wrapper.Verify(x => x.CheckCredentialCapability(It.IsAny<CheckCredentialCapabilityRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task LegacyRoute_OnlyAuthorizedUsersRedirectBeforeReading(bool allowed)
    {
        var wrapper = CapabilityWrapper(allowed);
        var page = Page(Principal(), wrapper.Object);
        page.TenantId = Guid.Empty;
        var tenant = new Mock<IPortalTenantContext>();
        tenant.SetupGet(x => x.SelectedTenantId).Returns(TenantId);
        SetProperty(page, "TenantFilter", tenant.Object);
        var navigation = new RecordingNavigation();
        SetProperty(page, "Navigation", navigation);
        await Invoke(page, "OnParametersSetAsync");
        navigation.LastUri.Should().Be(allowed ? $"/identity/tenants/{TenantId}/reference-data" : null);
        typeof(ReferenceData).GetField("_loadedTenantId", PrivateInstance)!.GetValue(page).Should().BeNull();
    }

    [TestCase("contact-types", true)]
    [TestCase("contact-groups", true)]
    [TestCase("address-types", true)]
    [TestCase("session-types", true)]
    [TestCase("wallet-types", false)]
    [TestCase("role-types", false)]
    [TestCase("role-type-groups", false)]
    [TestCase("currency-types", false)]
    [TestCase("exchange-rates", false)]
    public void ReferenceForm_FieldCount_OnlyShortFormsUseSingleColumnMarker(string tab, bool shortForm)
    {
        var page = new ReferenceData();
        typeof(ReferenceData).GetField("_activeTab", PrivateInstance)!.SetValue(page, tab);
        typeof(ReferenceData).GetProperty("IsShortForm", PrivateInstance)!.GetValue(page).Should().Be(shortForm);
    }

    [Test]
    public void Navigation_ReferenceDataIsOnlyLinkedFromAuthorizedTenantSettings()
    {
        var root = new DirectoryInfo(Environment.GetEnvironmentVariable("XFRAMEWORK_TEST_REPOSITORY_ROOT")
            ?? TestContext.CurrentContext.TestDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "XFramework.slnx"))) root = root.Parent;
        var layout = Path.Combine(root!.FullName, "src/Presentation/XFramework.Portal/Components/Layout");
        File.ReadAllText(Path.Combine(layout, "NavMenu.razor")).Should().NotContain("/admin/reference-data");
        var sidebar = File.ReadAllText(Path.Combine(layout, "TenantDetailSidebar.razor"));
        sidebar.Should().Contain("@if (_canManageReferenceData)").And.Contain("SectionHref(\"reference-data\")");
        typeof(ReferenceData).GetCustomAttributes<RouteAttribute>().Select(x => x.Template)
            .Should().Contain("/identity/tenants/{TenantId:guid}/reference-data").And.Contain("/admin/reference-data");
    }

    [TestCase("Categories")]
    [TestCase("Suppliers")]
    public void InventoryTable_UsesNativeSearchFiltersAndPaging(string page)
    {
        var root = new DirectoryInfo(Environment.GetEnvironmentVariable("XFRAMEWORK_TEST_REPOSITORY_ROOT")
            ?? TestContext.CurrentContext.TestDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "XFramework.slnx"))) root = root.Parent;
        var source = File.ReadAllText(Path.Combine(root!.FullName,
            "src/Presentation/XFramework.Portal.Features.Inventario/Pages", page + ".razor"));
        source.Should().Contain("ShowSearch=\"true\"").And.Contain("ShowPagination=\"true\"")
            .And.Contain("InitialPageSize=\"20\"").And.Contain("Filterable=\"true\"");
        source.Should().NotContain("@bind-Value=\"_search\"");
    }

    private static ReferenceData Page(ClaimsPrincipal user, IIdentityServerServiceWrapper wrapper)
    {
        var page = new ReferenceData { TenantId = TenantId };
        SetProperty(page, "IdentityServer", wrapper);
        SetProperty(page, "AuthenticationStateTask", Task.FromResult(new AuthenticationState(user)));
        SetProperty(page, "Logger", NullLogger<ReferenceData>.Instance);
        SetProperty(page, "DataContext", new Mock<IDataContext>(MockBehavior.Strict).Object);
        return page;
    }

    private static ClaimsPrincipal Principal(bool superUser = false, Guid? actorTenantId = null) => new(new ClaimsIdentity(
    [
        new Claim(PortalAuthClaims.TenantId, (actorTenantId ?? TenantId).ToString()),
        new Claim(PortalAuthClaims.CredentialId, CredentialId.ToString()),
        new Claim(PortalAuthClaims.RoleTypeId, Guid.NewGuid().ToString()),
        new Claim(PortalAuthClaims.IsSuperUser, superUser.ToString())
    ], "test"));

    private static Mock<IIdentityServerServiceWrapper> CapabilityWrapper(bool allowed)
    {
        var wrapper = new Mock<IIdentityServerServiceWrapper>(MockBehavior.Strict);
        wrapper.Setup(x => x.CheckCredentialCapability(It.IsAny<CheckCredentialCapabilityRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QueryResponse<CredentialCapabilityCheckResponse>
            {
                HttpStatusCode = HttpStatusCode.OK,
                Response = new() { TenantId = TenantId, CredentialId = CredentialId, IsAllowed = allowed }
            });
        return wrapper;
    }

    private static void SetProperty(ReferenceData page, string name, object value) =>
        typeof(ReferenceData).GetProperty(name, PrivateInstance)!.SetValue(page, value);

    private static Task Invoke(ReferenceData page, string name) =>
        (Task)typeof(ReferenceData).GetMethod(name, PrivateInstance)!.Invoke(page, null)!;

    private sealed class RecordingNavigation : NavigationManager
    {
        public string? LastUri { get; private set; }
        public RecordingNavigation() => Initialize("http://localhost/", "http://localhost/admin/reference-data");
        protected override void NavigateToCore(string uri, bool forceLoad) => LastUri = uri;
        protected override void NavigateToCore(string uri, NavigationOptions options) => LastUri = uri;
    }
}
