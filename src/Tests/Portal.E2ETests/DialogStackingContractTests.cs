using FluentAssertions;

namespace Portal.E2ETests;

[TestFixture]
public sealed class DialogStackingContractTests
{
    [Test]
    public void MainLayout_ServiceConfirmationsRenderAfterParentDialogPortals()
    {
        var root = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "CLAUDE.md")))
            root = root.Parent;
        root.Should().NotBeNull();
        var layout = File.ReadAllText(Path.Combine(root!.FullName,
            "src/Presentation/XFramework.Portal/Components/Layout/MainLayout.razor"));
        var confirmation = layout.IndexOf("<BbDialogProvider", StringComparison.Ordinal);
        var container = layout.IndexOf("<XfContainerPortalHost", StringComparison.Ordinal);
        var overlay = layout.IndexOf("<BbOverlayPortalHost", StringComparison.Ordinal);
        container.Should().BeGreaterThanOrEqualTo(0);
        overlay.Should().BeGreaterThanOrEqualTo(0);
        confirmation.Should().BeGreaterThan(container);
        confirmation.Should().BeGreaterThan(overlay);
    }
}
