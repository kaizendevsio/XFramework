using System.Linq.Expressions;
using System.Net;
using System.Reflection;
using FluentAssertions;
using Inventario.Integration.Drivers;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.DataContext;
using XFramework.Inventario.Domain.Shared.Contracts;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Brands;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Setup;
using XFramework.Inventario.Domain.Shared.Contracts.Responses;
using XFramework.Portal.Features.Inventario.Pages;
using XFramework.Portal.Shared;

namespace Portal.E2ETests;

[TestFixture]
[Category("Area:PortalContract"), Category("Module:Inventario"), Category("Area:Brands")]
public sealed class BrandsPageTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private Guid _tenant;
    private Mock<IPortalTenantContext> _context = null!;
    private Mock<IPortalModuleAvailability> _availability = null!;
    private Mock<IInventarioServiceWrapper> _wrapper = null!;
    private Mock<IDataContext> _data = null!;
    private Mock<IRemoteQuery<ProductCategory>> _query = null!;
    private ProductCategory _category = null!;

    [SetUp]
    public void Setup()
    {
        _tenant = Guid.NewGuid();
        _context = new();
        _context.SetupGet(x => x.SelectedTenantId).Returns(() => _tenant);
        _availability = new();
        _availability.SetupGet(x => x.ActiveTenantId).Returns(() => _tenant);
        _availability.Setup(x => x.EnsureLoadedAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _availability.Setup(x => x.IsFeatureEnabled(It.IsAny<string>(), It.IsAny<string?>())).Returns(true);
        _wrapper = new(MockBehavior.Strict);
        _wrapper.Setup(x => x.SearchProductBrands(It.IsAny<SearchProductBrandsRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QueryResponse<List<string>> { HttpStatusCode = HttpStatusCode.OK, Response = ["ACME"] });
        _data = new(MockBehavior.Strict);
        _query = new();
        _query.Setup(x => x.Where(It.IsAny<Expression<Func<ProductCategory, bool>>>())).Returns(_query.Object);
        _query.Setup(x => x.NoCache()).Returns(_query.Object);
        _query.Setup(x => x.OrderBy(It.IsAny<Expression<Func<ProductCategory, string?>>>())).Returns(_query.Object);
        _query.Setup(x => x.Take(It.IsAny<int>())).Returns(_query.Object);
        _category = new() { Id = Guid.NewGuid(), TenantId = _tenant, Name = "Category" };
        _query.Setup(x => x.ToListAsync(It.IsAny<CancellationToken>())).ReturnsAsync([_category]);
        _data.Setup(x => x.Query<ProductCategory>()).Returns(_query.Object);
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task Reload_PermissionsComeFromCurrentTenantPreferences_AndCategoriesAreBounded(bool manager)
    {
        _wrapper.Setup(x => x.GetInventarioSetup(It.IsAny<GetInventarioSetupRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response(_tenant, manager));
        using var page = Page();
        await Reload(page);
        typeof(Brands).GetProperty("CanManage", Private)!.GetValue(page).Should().Be(manager);
        _wrapper.Verify(x => x.GetInventarioSetup(It.Is<GetInventarioSetupRequest>(r => r.Metadata.RequestedTenantId == _tenant),
            It.IsAny<CancellationToken>()), Times.Once);
        _query.Verify(x => x.Take(1000), Times.Once);
        await (Task)typeof(Brands).GetMethod("SelectCategory", Private)!.Invoke(page, [_category.Id.ToString()])!;
        typeof(Brands).GetMethod("OpenMerge", Private)!.Invoke(page, new object?[] { null });
        Field<bool>(page, "_mergeOpen").Should().Be(manager);
        _data.Verify(x => x.Query<ProductCategory>(), Times.Once);
        _query.Verify(x => x.ToListAsync(It.IsAny<CancellationToken>()), Times.Once);
        _data.VerifyNoOtherCalls();
    }

    [Test]
    public async Task Reload_WrongTenantPreferences_DoesNotEnableMergeOrReadCategories()
    {
        _wrapper.Setup(x => x.GetInventarioSetup(It.IsAny<GetInventarioSetupRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response(Guid.NewGuid(), true));
        using var page = Page();
        await Reload(page);
        Field<InventarioSetupResponse?>(page, "_preferences").Should().BeNull();
        Field<string?>(page, "_error").Should().NotBeNull();
        _data.VerifyNoOtherCalls();
    }

    [Test]
    public async Task Reload_TenantSwitchDuringRead_DiscardsOldPermissionAndCategoryState()
    {
        var tenant = _tenant;
        var pending = new TaskCompletionSource<QueryResponse<InventarioSetupResponse>>(TaskCreationOptions.RunContinuationsAsynchronously);
        _wrapper.Setup(x => x.GetInventarioSetup(It.IsAny<GetInventarioSetupRequest>(), It.IsAny<CancellationToken>())).Returns(pending.Task);
        using var page = Page();
        var loading = Reload(page);
        _tenant = Guid.NewGuid();
        pending.SetResult(Response(tenant, true));
        await loading;
        Field<InventarioSetupResponse?>(page, "_preferences").Should().BeNull();
        _data.VerifyNoOtherCalls();
    }

    [Test]
    public async Task Reload_DisabledCatalog_DoesNotLoadPermissionsOrCategories()
    {
        _availability.Setup(x => x.IsFeatureEnabled(It.IsAny<string>(), It.IsAny<string?>())).Returns(false);
        using var page = Page();
        await Reload(page);
        _wrapper.VerifyNoOtherCalls();
        _data.VerifyNoOtherCalls();
    }

    [Test]
    public async Task Brands_SearchUsesCurrentTenantCategoryAndBoundedWrapper()
    {
        _wrapper.Setup(x => x.GetInventarioSetup(It.IsAny<GetInventarioSetupRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response(_tenant, true));
        using var page = Page();
        await Reload(page);
        await (Task)typeof(Brands).GetMethod("SearchBrands", Private)!.Invoke(page, ["ACME"] )!;
        _wrapper.Verify(x => x.SearchProductBrands(It.Is<SearchProductBrandsRequest>(r =>
            r.Metadata.RequestedTenantId == _tenant && r.CategoryId == _category.Id && r.Search == "ACME" && r.Limit == 50),
            It.IsAny<CancellationToken>()), Times.Once);
        typeof(Brands).GetMethod("OpenMerge", Private)!.Invoke(page, ["ACME"]);
        Field<string>(page, "_mergeSource").Should().Be("ACME");
    }

    [Test]
    public async Task Brands_FailedReadShowsErrorAndRetryClearsIt()
    {
        _wrapper.Setup(x => x.GetInventarioSetup(It.IsAny<GetInventarioSetupRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response(_tenant, true));
        _wrapper.SetupSequence(x => x.SearchProductBrands(It.IsAny<SearchProductBrandsRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QueryResponse<List<string>> { HttpStatusCode = HttpStatusCode.Forbidden })
            .ReturnsAsync(new QueryResponse<List<string>> { HttpStatusCode = HttpStatusCode.OK, Response = ["ACME"] });
        using var page = Page();
        await Reload(page);
        Field<string?>(page, "_brandsError").Should().NotBeNull();
        await (Task)typeof(Brands).GetMethod("LoadBrands", Private)!.Invoke(page, null)!;
        Field<string?>(page, "_brandsError").Should().BeNull();
    }

    [Test]
    public async Task Brands_TenantSwitchDuringReadDiscardsOldRows()
    {
        _wrapper.Setup(x => x.GetInventarioSetup(It.IsAny<GetInventarioSetupRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response(_tenant, true));
        var pending = new TaskCompletionSource<QueryResponse<List<string>>>(TaskCreationOptions.RunContinuationsAsynchronously);
        _wrapper.Setup(x => x.SearchProductBrands(It.IsAny<SearchProductBrandsRequest>(), It.IsAny<CancellationToken>())).Returns(pending.Task);
        using var page = Page();
        var loading = Reload(page);
        _tenant = Guid.NewGuid();
        pending.SetResult(new() { HttpStatusCode = HttpStatusCode.OK, Response = ["FOREIGN BRAND"] });
        await loading;
        ((System.Collections.ICollection)typeof(Brands).GetField("_brands", Private)!.GetValue(page)!).Count.Should().Be(0);
    }

    [Test]
    public async Task Brands_OutOfOrderSearchDoesNotReplaceNewerRows()
    {
        _wrapper.Setup(x => x.GetInventarioSetup(It.IsAny<GetInventarioSetupRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response(_tenant, true));
        using var page = Page();
        await Reload(page);
        var pending = new TaskCompletionSource<QueryResponse<List<string>>>(TaskCreationOptions.RunContinuationsAsynchronously);
        _wrapper.Setup(x => x.SearchProductBrands(It.Is<SearchProductBrandsRequest>(r => r.Search == "OLD"), It.IsAny<CancellationToken>()))
            .Returns(pending.Task);
        var search = typeof(Brands).GetMethod("SearchBrands", Private)!;
        var oldRead = (Task)search.Invoke(page, ["OLD"])!;
        await (Task)search.Invoke(page, ["ACME"])!;
        pending.SetResult(new() { HttpStatusCode = HttpStatusCode.OK, Response = ["OLD"] });
        await oldRead;
        var rows = (System.Collections.IEnumerable)typeof(Brands).GetField("_brands", Private)!.GetValue(page)!;
        rows.Cast<object>().Select(row => row.GetType().GetProperty("Name")!.GetValue(row)).Should().Equal("ACME");
    }

    private Brands Page()
    {
        var page = new Brands();
        typeof(Brands).GetProperty("Inventario", Private)!.SetValue(page, _wrapper.Object);
        typeof(Brands).GetProperty("DataContext", Private)!.SetValue(page, _data.Object);
        typeof(Brands).GetProperty("TenantContext", Private)!.SetValue(page, _context.Object);
        typeof(Brands).GetProperty("Availability", Private)!.SetValue(page, _availability.Object);
        typeof(Brands).GetProperty("Logger", Private)!.SetValue(page, NullLogger<Brands>.Instance);
        return page;
    }
    private static Task Reload(Brands page) => (Task)typeof(Brands).GetMethod("Reload", Private)!.Invoke(page, null)!;
    private static T Field<T>(Brands page, string field) => (T)typeof(Brands).GetField(field, Private)!.GetValue(page)!;
    private static QueryResponse<InventarioSetupResponse> Response(Guid tenant, bool manager) =>
        new() { HttpStatusCode = HttpStatusCode.OK, Response = new() { TenantId = tenant, CanManage = manager } };
}

