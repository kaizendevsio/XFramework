using FluentAssertions;
using System.Reflection;
using Microsoft.AspNetCore.Components;
using XFramework.Portal.Components.Layout;

namespace Portal.E2ETests;

[TestFixture]
public sealed class PortalSidebarStateTests
{
    [TestCase("/identity/users", "identity")]
    [TestCase("/inventario/overview", "inventario")]
    [TestCase("/pos/reports", "pos")]
    [TestCase("/finance/wallets", "finance")]
    [TestCase("/audit/logs", "audit")]
    [TestCase("/storage/files", "storage")]
    [TestCase("/identity/users/00000000-0000-0000-0000-000000000001/roles", "user-detail")]
    public void RouteActivation_ExpandsMatchingGroup(string route, string group)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var layout = new MainLayout();
        typeof(MainLayout).GetProperty("Nav", flags)!.SetValue(layout, new RouteNavigation(route));
        typeof(MainLayout).GetMethod("UpdateActiveSidebarGroup", flags)!.Invoke(layout, null);
        var state = (PortalSidebarState)typeof(MainLayout).GetField("_sidebarState", flags)!.GetValue(layout)!;
        state.OpenGroupIds.Should().Equal(group);
    }

    private sealed class RouteNavigation : NavigationManager
    {
        public RouteNavigation(string route) => Initialize("https://portal.test/", "https://portal.test" + route);
    }
    [TestCase("identity")]
    [TestCase("inventario")]
    [TestCase("pos")]
    [TestCase("finance")]
    public void MainGroup_ActivationExpandsOnlyActiveGroup(string group)
    {
        var state = new PortalSidebarState([]);
        state.ActivateGroup(group);
        state.OpenGroupIds.Should().Equal(group);
        state.ActiveGroupId.Should().Be(group);
    }

    [TestCase("tenant-detail")]
    [TestCase("wallet-detail")]
    [TestCase("user-detail")]
    [TestCase("product-detail")]
    [TestCase("identity")]
    [TestCase("inventario")]
    [TestCase("pos")]
    public void DetailGroup_OpensOnEntry_ButRespectsManualCollapse(string group)
    {
        var state = new PortalSidebarState([]);
        state.ActivateGroup(group);
        state.IsGroupOpen(group).Should().BeTrue();
        state.SetGroupOpen(group, false);
        state.ActivateGroup(group);
        state.IsGroupOpen(group).Should().BeFalse();
        state.ActivateGroup("finance");
        state.ActivateGroup(group);
        state.IsGroupOpen(group).Should().BeTrue();
    }

    [Test]
    public void TenantChange_ClearsExpandedMainGroups_AndReopensOnlyDetail()
    {
        var state = new PortalSidebarState(["identity", "inventario", "finance"]);
        state.ResetGroups();
        state.OpenGroupIds.Should().BeEmpty();
        state.ActivateGroup("tenant-detail");
        state.OpenGroupIds.Should().Equal("tenant-detail");
    }

    [Test]
    public void ActivateGroup_ModuleChange_ClosesPreviousActiveGroup()
    {
        var state = new PortalSidebarState([]);
        state.ActivateGroup("identity");
        state.ActivateGroup("inventario");
        state.OpenGroupIds.Should().Equal("inventario");
        state.ActivateGroup(null);
        state.OpenGroupIds.Should().BeEmpty();
    }
}
