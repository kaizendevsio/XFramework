using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Net;
using FluentAssertions;
using Inventario.Integration.Drivers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;
using Moq;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.DataContext;
using XFramework.Integration.DataContext.ExpressionVisitor;
using XFramework.Inventario.Domain.Shared.Contracts;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Planning;
using XFramework.Inventario.Domain.Shared.Contracts.Responses.Reports;

namespace Portal.E2ETests;

public sealed partial class InventoryReportsE2ETests
{
    [Test]
    public async Task Planning_OverviewDrilldown_PreservesWarehouseAndUsesServerPages_1920()
    {
        await Page.SetViewportSizeAsync(1920, 1080);
        await OpenScopedOverview();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Low stock 1,207", Exact = true }).ClickAsync();
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Replenishment Planning" })).ToBeVisibleAsync();
        await Expect(Page.GetByText("Replenishment 0001", new() { Exact = true })).ToBeVisibleAsync();
        await Expect(Page.Locator("[title='Low-stock positions matching active reorder rules']")).ToHaveTextAsync("1,207");
        await Expect(Page.GetByText("MAIN - Main warehouse", new() { Exact = true }).First).ToBeVisibleAsync();
        state.PlanningRequests.Last().WarehouseId.Should().Be(state.WarehouseId);
        state.PlanningRequests.Last().Metadata.RequestedTenantId.Should().Be(state.FirstTenant);
        state.PlanningRequests.Last().Take.Should().Be(20);
        var directory = Path.Combine(TestContext.CurrentContext.WorkDirectory, "artifacts", "inventory-planning");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "planning-scoped-1920.png");
        await Page.ScreenshotAsync(new() { Path = path, Animations = ScreenshotAnimations.Disabled });
        TestContext.AddTestAttachment(path);
        (await Page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= innerWidth")).Should().BeTrue();
        TestContext.Progress.WriteLine(await Page.GetByRole(AriaRole.Tabpanel).First.AriaSnapshotAsync());
        await Page.GetByRole(AriaRole.Button, new() { NameRegex = new("^Next( page)?$", System.Text.RegularExpressions.RegexOptions.IgnoreCase) }).First.ClickAsync();
        await Expect(Page.GetByText("Replenishment 0021", new() { Exact = true })).ToBeVisibleAsync();
        state.PlanningRequests.Last().Skip.Should().Be(20);
        state.PlanningRequests.Last().WarehouseId.Should().Be(state.WarehouseId);
    }

    [TestCase("not-a-guid")]
    [TestCase("00000000-0000-0000-0000-000000000000")]
    [TestCase("11111111-1111-1111-1111-111111111111")]
    public async Task Planning_InvalidOrForeignWarehouse_DoesNotBroadenScope(string warehouse)
    {
        await Page.GotoAsync(app.Urls.Single() + "/inventario/planning?warehouseId=" + warehouse);
        await Expect(Page.GetByRole(AriaRole.Alert)).ToContainTextAsync(warehouse.StartsWith("111") ? "Warehouse is unavailable" : "Invalid warehouse scope");
        state.PlanningRequests.Should().BeEmpty();
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Replenishment Planning" })).ToHaveCountAsync(0);
    }

    [Test]
    public async Task Planning_TenantChange_DiscardsStalePageAndRevalidatesWarehouse()
    {
        var hold = state.HoldPlanning = new(TaskCreationOptions.RunContinuationsAsynchronously);
        await Page.GotoAsync(app.Urls.Single() + $"/inventario/planning?warehouseId={state.WarehouseId}");
        await Wait(() => !state.PlanningRequests.IsEmpty);
        state.SelectedTenant = state.SecondTenant;
        state.Tenant.Raise(x => x.OnChanged += null);
        hold.TrySetResult();
        await Expect(Page.GetByRole(AriaRole.Alert)).ToContainTextAsync("Warehouse is unavailable");
        await Expect(Page.GetByText("Replenishment 0001", new() { Exact = true })).ToHaveCountAsync(0);
        state.PlanningRequests.Should().OnlyContain(x => x.Metadata.RequestedTenantId == state.FirstTenant);
    }

    [Test]
    public async Task Planning_DeniedPage_ShowsUnavailableAndRefreshRecovers()
    {
        state.PlanningStatus = HttpStatusCode.Forbidden;
        await Page.GotoAsync(app.Urls.Single() + "/inventario/planning");
        await Expect(Page.GetByRole(AriaRole.Alert)).ToContainTextAsync("Could not load suggestions");
        await Expect(Page.Locator("[title='Low-stock positions matching active reorder rules']")).ToHaveTextAsync("Unavailable");
        state.PlanningStatus = HttpStatusCode.OK;
        await Page.GetByRole(AriaRole.Button, new() { Name = "Refresh", Exact = true }).ClickAsync();
        await Expect(Page.GetByText("Replenishment 0001", new() { Exact = true })).ToBeVisibleAsync();
    }

    private void ConfigurePlanningFixture(IServiceCollection services, Mock<IInventarioServiceWrapper> wrapper)
    {
        wrapper.Setup(x => x.GetReorderSuggestionPage(It.IsAny<GetReorderSuggestionPageRequest>(), It.IsAny<CancellationToken>()))
            .Returns((GetReorderSuggestionPageRequest request, CancellationToken ct) => state.LoadPlanning(request));
        var data = new Mock<IDataContext>(MockBehavior.Strict);
        data.Setup(x => x.Query<Product>()).Returns(() => PlanningQuery<Product>([]));
        data.Setup(x => x.Query<ProductVariation>()).Returns(() => PlanningQuery<ProductVariation>([]));
        data.Setup(x => x.Query<InventoryLocation>()).Returns(() => PlanningQuery<InventoryLocation>([]));
        data.Setup(x => x.Query<InventoryReorderRule>()).Returns(() => PlanningQuery(Enumerable.Range(0, 1207).Select(_ => new InventoryReorderRule
            { Id = Guid.NewGuid(), TenantId = state.FirstTenant, WarehouseId = state.WarehouseId, IsActive = true })));
        data.Setup(x => x.Query<Warehouse>()).Returns(() => PlanningQuery<Warehouse>([
            new() { Id = state.WarehouseId, TenantId = state.FirstTenant, Code = "MAIN", Name = "Main warehouse" }]));
        services.AddSingleton(data.Object);
    }

    private static IRemoteQuery<T> PlanningQuery<T>(IEnumerable<T> rows) where T : class
    {
        var query = new Mock<IRemoteQuery<T>>(MockBehavior.Strict);
        query.Setup(x => x.NoCache()).Returns(query.Object);
        query.Setup(x => x.Where(It.IsAny<Expression<Func<T, bool>>>())).Returns((Expression<Func<T, bool>> predicate) =>
        {
            // Exercise the production serializer, not just LINQ-to-Objects semantics.
            QueryExpressionVisitor.Parse(predicate);
            return PlanningQuery(rows.Where(predicate.Compile()));
        });
        query.Setup(x => x.Take(It.IsAny<int>())).Returns((int count) => PlanningQuery(rows.Take(count)));
        query.Setup(x => x.OrderBy(It.IsAny<Expression<Func<T, string>>>())).Returns(query.Object);
        query.Setup(x => x.OrderBy(It.IsAny<Expression<Func<T, DateTime>>>())).Returns(query.Object);
        query.Setup(x => x.CountAsync(It.IsAny<CancellationToken>())).ReturnsAsync(() => rows.Count());
        query.Setup(x => x.FirstOrDefaultAsync(It.IsAny<CancellationToken>())).ReturnsAsync(() => rows.FirstOrDefault());
        query.Setup(x => x.ToListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(() => rows.ToList());
        return query.Object;
    }
}

public sealed partial class InventoryReportFixtureState
{
    public ConcurrentQueue<GetReorderSuggestionPageRequest> PlanningRequests { get; } = new();
    public HttpStatusCode PlanningStatus = HttpStatusCode.OK;
    public TaskCompletionSource? HoldPlanning;
    public async Task<QueryResponse<ReorderSuggestionPage>> LoadPlanning(GetReorderSuggestionPageRequest request)
    {
        var hold = Interlocked.Exchange(ref HoldPlanning, null);
        PlanningRequests.Enqueue(request);
        if (hold is not null) await hold.Task;
        return new() { HttpStatusCode = PlanningStatus, Response = PlanningStatus != HttpStatusCode.OK ? null : new(1207, 18105,
            Enumerable.Range(request.Skip + 1, Math.Min(request.Take, 1207 - request.Skip)).Select(i =>
                new ReorderSuggestionRow(ProductId, $"Replenishment {i:0000}", null, null, null, WarehouseId, "Main warehouse",
                    LocationId, $"Shelf {i}", 0, 5, 15, "Acme Supply")).ToList()) };
    }
}
