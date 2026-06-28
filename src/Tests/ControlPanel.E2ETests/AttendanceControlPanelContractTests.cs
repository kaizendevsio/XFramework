using FluentAssertions;

namespace ControlPanel.E2ETests;

[TestFixture]
[Category("Kind:Integration")]
[Category("Module:Attendance")]
[Category("Area:ControlPanelContract")]
public sealed class AttendanceControlPanelContractTests
{
    [Test]
    public void AttendanceNavigation_IsFeatureGatedAndUserDetailLinked()
    {
        var repositoryRoot = FindRepositoryRoot();
        var layoutRoot = Path.Combine(repositoryRoot.FullName, "src", "Presentation", "ControlPanel.Server", "Components", "Layout");
        var identityRoot = Path.Combine(repositoryRoot.FullName, "src", "Presentation", "ControlPanel.Server", "Components", "Pages", "Identity");

        var navMenu = File.ReadAllText(Path.Combine(layoutRoot, "NavMenu.razor"));
        var mainLayout = File.ReadAllText(Path.Combine(layoutRoot, "MainLayout.razor"));
        var userSidebar = File.ReadAllText(Path.Combine(layoutRoot, "UserDetailSidebar.razor"));
        var userDetail = File.ReadAllText(Path.Combine(identityRoot, "UserDetail.razor"));

        navMenu.Should().Contain("TenantModuleFeatureKeys.Attendance");
        navMenu.Should().Contain("Href=\"/attendance/contexts\"");
        navMenu.Should().Contain("Href=\"/attendance/sessions\"");
        navMenu.Should().Contain("Href=\"/attendance/reports\"");
        mainLayout.Should().Contain("\"attendance\" => \"attendance\"");

        userSidebar.Should().Contain("SectionHref(\"attendance\")");
        userDetail.Should().Contain("case \"attendance\"");
        userDetail.Should().Contain("LoadAttendance()");
        userDetail.Should().Contain("ModuleUnavailable ModuleName=\"Attendance\"");
    }

    [Test]
    public void AttendancePages_UseWrapperForBusinessMutations()
    {
        var source = ReadAttendancePageSource();

        source.Should().Contain("[Inject] private IAttendanceServiceWrapper Attendance");
        source.Should().Contain("Attendance.CreateAttendanceContext(new CreateAttendanceContextRequest");
        source.Should().Contain("Attendance.UpdateAttendanceContext(new UpdateAttendanceContextRequest");
        source.Should().Contain("Attendance.CreateAttendanceSession(new CreateAttendanceSessionRequest");
        source.Should().Contain("Attendance.AddAttendanceParticipant(new AddAttendanceParticipantRequest");
        source.Should().Contain("Attendance.RemoveAttendanceParticipant(new RemoveAttendanceParticipantRequest");
        source.Should().Contain("Attendance.RecordAttendanceEvent(new RecordAttendanceEventRequest");
        source.Should().Contain("Attendance.CreateAttendanceAdjustment(new CreateAttendanceAdjustmentRequest");
        source.Should().Contain("Attendance.GetAttendanceReport(new GetAttendanceReportRequest");

        source.Should().NotContain("DataContext.Add(");
        source.Should().NotContain("DataContext.Update(");
        source.Should().NotContain("DataContext.Remove(");
        source.Should().NotContain("SaveChangesAsync(");
    }

    [Test]
    public void AttendancePages_UseControlPanelGridAndPickerConventions()
    {
        var source = ReadAttendancePageSource();

        source.Should().Contain("<BbDataGrid");
        source.Should().Contain("Filterable=\"true\"");
        source.Should().Contain("<EmptyTemplate>");
        source.Should().Contain("<XfEntityPicker TItem=\"AttendanceContextOption\"");
        source.Should().Contain("<XfEntityPicker TItem=\"IdentityCredential\"");
        source.Should().Contain("ModuleUnavailable ModuleName=\"Attendance\" RequiresTenant=\"true\"");
        source.Should().Contain("StatusBadge");

        source.Should().NotContain("<table", "Attendance list and report UI should use BbDataGrid");
        source.Should().NotContain("<BbFormFieldNativeSelect");
    }

    [Test]
    public void AttendanceSessionDetail_UsesManualSourceActorReasonAndIdempotency()
    {
        var sessionDetail = File.ReadAllText(Path.Combine(GetAttendancePagesRoot(), "SessionDetail.razor"));

        sessionDetail.Should().Contain("Source = AttendanceEventSource.Manual");
        sessionDetail.Should().Contain("RecordedByCredentialId = actorCredentialId");
        sessionDetail.Should().Contain("IdempotencyKey = $\"controlpanel:");
        sessionDetail.Should().Contain("ActorCredentialId = actorCredentialId");
        sessionDetail.Should().Contain("Reason = _adjustmentForm.Reason.Trim()");
        sessionDetail.Should().Contain("ManualActionsDisabled => RequestMetadata.CredentialId is null");
    }

    [Test]
    public void AttendanceReadService_UsesApprovedTenantScopedReadProjections()
    {
        var repositoryRoot = FindRepositoryRoot();
        var servicePath = Path.Combine(
            repositoryRoot.FullName,
            "src",
            "Presentation",
            "ControlPanel.Server",
            "Services",
            "AttendanceControlPanelReadService.cs");
        var service = File.ReadAllText(servicePath);

        service.Should().Contain("IDataContext dataContext");
        service.Should().Contain("IAttendanceServiceWrapper attendance");
        service.Should().Contain("attendance.GetAttendanceContexts(new GetAttendanceContextsRequest");
        service.Should().Contain("dataContext.Query<AttendanceContext>()");
        service.Should().Contain("dataContext.Query<AttendanceSession>()");
        service.Should().Contain("dataContext.Query<AttendanceParticipant>()");
        service.Should().Contain("dataContext.Query<AttendanceRecord>()");
        service.Should().Contain("dataContext.Query<IdentityCredential>()");
        service.Should().Contain("x.TenantId == tenantId");
        service.Should().Contain("BuildCredentialLabel");
        service.Should().Contain("AttendanceRecordStatus.Absent");
        service.Should().Contain("NormalizeUtc(fromUtc)");
        service.Should().Contain("context.Id != Guid.Empty");
        service.Should().NotContain("x.StartsAt >= fromUtc");
        service.Should().NotContain("SaveChangesAsync(");
    }

    [Test]
    public void AttendanceApi_RegistersGeneratedBoltHandlersAtStartup()
    {
        var repositoryRoot = FindRepositoryRoot();
        var programPath = Path.Combine(
            repositoryRoot.FullName,
            "src",
            "Modules",
            "XFramework.Attendance",
            "Attendance.Api",
            "Program.cs");
        var program = File.ReadAllText(programPath);

        program.Should().Contain("BoltHandlerRegistry.RegisterAll");
        program.Should().Contain("CreateLogger(\"Attendance.GeneratedBoltHandlers\")");
    }

    private static string ReadAttendancePageSource()
    {
        var pagesRoot = GetAttendancePagesRoot();
        var pages = new[]
        {
            "Contexts.razor",
            "Sessions.razor",
            "SessionDetail.razor",
            "Reports.razor"
        };

        return string.Join(Environment.NewLine, pages.Select(page => File.ReadAllText(Path.Combine(pagesRoot, page))));
    }

    private static string GetAttendancePagesRoot()
    {
        var repositoryRoot = FindRepositoryRoot();
        return Path.Combine(repositoryRoot.FullName, "src", "Presentation", "ControlPanel.Server", "Components", "Pages", "Attendance");
    }

    private static DirectoryInfo FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "XFramework.slnx")))
            {
                return directory;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the XFramework repository root.");
    }
}
