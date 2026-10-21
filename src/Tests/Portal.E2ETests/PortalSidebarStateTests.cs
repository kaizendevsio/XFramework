using FluentAssertions;
using XFramework.Portal.Components.Layout;

namespace Portal.E2ETests;

[TestFixture]
public sealed class PortalSidebarStateTests
{
    [TestCase("identity")]
    [TestCase("inventario")]
    [TestCase("pos")]
    [TestCase("finance")]
    public void MainGroup_ActivationDoesNotExpand(string group)
    {
        var state = new PortalSidebarState([]);
        state.ActivateGroup(group);
        state.OpenGroupIds.Should().BeEmpty();
        state.ActiveGroupId.Should().Be(group);
    }

    [TestCase("tenant-detail")]
    [TestCase("wallet-detail")]
    [TestCase("user-detail")]
    [TestCase("product-detail")]
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
}
