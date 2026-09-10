using FluentAssertions;
using XFramework.Portal.Components.Layout;

namespace Portal.E2ETests;

[TestFixture]
public sealed class AuditBreadcrumbTests
{
    [TestCase("audit/logs/43853")]
    [TestCase("/audit/logs/43853/")]
    public void BuildAuditBreadcrumbs_EventDetail_LinksParentsButNotCurrentEvent(string path)
    {
        var crumbs = MainLayout.BuildAuditBreadcrumbs(path);

        crumbs.Should().Equal(
            ("Audit", "/audit/logs"),
            ("Logs", "/audit/logs"),
            ("43853", (string?)null));
    }

    [Test]
    public void BuildAuditBreadcrumbs_List_KeepsLogsAsCurrentPage()
    {
        MainLayout.BuildAuditBreadcrumbs("audit/logs").Should().Equal(
            ("Audit", "/audit/logs"),
            ("Logs", (string?)null));
    }
}
