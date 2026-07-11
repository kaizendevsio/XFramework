using FluentAssertions;

namespace Portal.E2ETests;

[TestFixture]
[Category("Kind:Integration")]
[Category("Module:IdentityServer")]
[Category("Area:PortalContract")]
public sealed class IdentityServerPortalContractTests
{
    [Test]
    public void IdentityServerEntityRelationshipDialogs_UseSharedEntityPickerAdvancedSearch()
    {
        var repositoryRoot = FindRepositoryRoot();
        var pagesRoot = Path.Combine(
            repositoryRoot.FullName,
            "src",
            "Presentation",
            "XFramework.Portal",
            "Components",
            "Pages",
            "Identity");

        var userDetail = File.ReadAllText(Path.Combine(pagesRoot, "UserDetail.razor"));
        var contacts = File.ReadAllText(Path.Combine(pagesRoot, "Contacts.razor"));
        var contactDetail = File.ReadAllText(Path.Combine(pagesRoot, "ContactDetail.razor"));
        var addresses = File.ReadAllText(Path.Combine(pagesRoot, "Addresses.razor"));
        var addressDetail = File.ReadAllText(Path.Combine(pagesRoot, "AddressDetail.razor"));
        var tenants = File.ReadAllText(Path.Combine(pagesRoot, "Tenants.razor"));
        var tenantDetail = File.ReadAllText(Path.Combine(pagesRoot, "TenantDetail.razor"));

        userDetail.Should().Contain("<XfEntityPicker TItem=\"IdentityCredential\"");
        userDetail.Should().Contain("<XfEntityPicker TItem=\"IdentityRoleType\"");
        userDetail.Should().Contain("<XfEntityPicker TItem=\"IdentityContactType\"");
        userDetail.Should().Contain("<XfEntityPicker TItem=\"IdentityContactGroup\"");
        userDetail.Should().Contain("AdvancedColumns=\"@CredentialAdvancedColumns\"");
        userDetail.Should().Contain("AdvancedFilters=\"@CredentialAdvancedFilters\"");
        userDetail.Should().Contain("AdvancedColumns=\"@RoleTypeAdvancedColumns\"");
        userDetail.Should().Contain("AdvancedFilters=\"@RoleTypeAdvancedFilters\"");
        userDetail.Should().Contain("AdvancedColumns=\"@ContactTypeAdvancedColumns\"");
        userDetail.Should().Contain("AdvancedFilters=\"@ContactTypeAdvancedFilters\"");
        userDetail.Should().Contain("AdvancedColumns=\"@ContactGroupAdvancedColumns\"");
        userDetail.Should().Contain("AdvancedFilters=\"@ContactGroupAdvancedFilters\"");

        contacts.Should().Contain("<XfEntityPicker TItem=\"IdentityCredential\"");
        contacts.Should().Contain("<XfEntityPicker TItem=\"IdentityContactType\"");
        contacts.Should().Contain("<XfEntityPicker TItem=\"IdentityContactGroup\"");
        contacts.Should().Contain("AdvancedColumns=\"@CredentialAdvancedColumns\"");
        contacts.Should().Contain("AdvancedFilters=\"@CredentialAdvancedFilters\"");
        contacts.Should().Contain("AdvancedColumns=\"@ContactTypeAdvancedColumns\"");
        contacts.Should().Contain("AdvancedFilters=\"@ContactTypeAdvancedFilters\"");
        contacts.Should().Contain("AdvancedColumns=\"@ContactGroupAdvancedColumns\"");
        contacts.Should().Contain("AdvancedFilters=\"@ContactGroupAdvancedFilters\"");
        contacts.Should().NotContain("Label=\"Credential ID\"");
        contacts.Should().NotContain("Label=\"Type ID\"");
        contacts.Should().NotContain("Label=\"Group ID\"");

        contactDetail.Should().Contain("<XfEntityPicker TItem=\"IdentityCredential\"");
        contactDetail.Should().Contain("<XfEntityPicker TItem=\"IdentityContactType\"");
        contactDetail.Should().Contain("<XfEntityPicker TItem=\"IdentityContactGroup\"");
        contactDetail.Should().Contain("AdvancedColumns=\"@CredentialAdvancedColumns\"");
        contactDetail.Should().Contain("AdvancedFilters=\"@CredentialAdvancedFilters\"");
        contactDetail.Should().Contain("AdvancedColumns=\"@ContactTypeAdvancedColumns\"");
        contactDetail.Should().Contain("AdvancedFilters=\"@ContactTypeAdvancedFilters\"");
        contactDetail.Should().Contain("AdvancedColumns=\"@ContactGroupAdvancedColumns\"");
        contactDetail.Should().Contain("AdvancedFilters=\"@ContactGroupAdvancedFilters\"");

        addresses.Should().Contain("<XfEntityPicker TItem=\"IdentityInformation\"");
        addresses.Should().Contain("AdvancedColumns=\"@UserAdvancedColumns\"");
        addresses.Should().Contain("AdvancedFilters=\"@UserAdvancedFilters\"");
        addresses.Should().NotContain("Label=\"User ID (IdentityInfoId)\"");

        addressDetail.Should().Contain("<XfEntityPicker TItem=\"IdentityInformation\"");
        addressDetail.Should().Contain("AdvancedColumns=\"@UserAdvancedColumns\"");
        addressDetail.Should().Contain("AdvancedFilters=\"@UserAdvancedFilters\"");
        addressDetail.Should().NotContain("Label=\"User ID (IdentityInfoId)\"");

        tenants.Should().Contain("<XfEntityPicker TItem=\"Tenant\"");
        tenants.Should().Contain("AdvancedColumns=\"@TenantAdvancedColumns\"");
        tenants.Should().Contain("AdvancedFilters=\"@TenantAdvancedFilters\"");
        tenants.Should().Contain("AdvancedSearchScope=\"Searches tenant name, description, status, version, expiration, enabled state, and created date.\"");
        tenants.Should().NotContain("Label=\"Parent Tenant ID\"");

        tenantDetail.Should().Contain("<XfEntityPicker TItem=\"RegistryConfigurationGroup\"");
        tenantDetail.Should().Contain("AdvancedColumns=\"@ConfigurationGroupAdvancedColumns\"");
        tenantDetail.Should().Contain("AdvancedFilters=\"@ConfigurationGroupAdvancedFilters\"");
        tenantDetail.Should().Contain("<XfEntityPicker TItem=\"Tenant\"");
        tenantDetail.Should().Contain("AdvancedColumns=\"@TenantAdvancedColumns\"");
        tenantDetail.Should().Contain("AdvancedFilters=\"@TenantAdvancedFilters\"");
        tenantDetail.Should().Contain("AdvancedSearchScope=\"Searches tenant name, description, status, version, expiration, enabled state, and created date.\"");
        tenantDetail.Should().NotContain("Placeholder=\"GUID or blank for root\"");
        tenantDetail.Should().NotContain("<BbFormFieldNativeSelect");
    }

    [Test]
    public void IdentityServerContactAndAddressLists_NavigateExistingRecordsToDetailPages()
    {
        var pagesRoot = GetIdentityPagesRoot();
        var contacts = File.ReadAllText(Path.Combine(pagesRoot, "Contacts.razor"));
        var addresses = File.ReadAllText(Path.Combine(pagesRoot, "Addresses.razor"));
        var userDetail = File.ReadAllText(Path.Combine(pagesRoot, "UserDetail.razor"));

        contacts.Should().Contain("@page \"/identity/contacts\"");
        contacts.Should().Contain("OnRowClick=\"@((IdentityContact item) => OpenContactDetail(item))\"");
        contacts.Should().Contain("Navigation.NavigateTo($\"/identity/contacts/{contact.Id}\")");
        contacts.Should().NotContain("OpenEditDialog");
        contacts.Should().NotContain("Edit Contact");

        addresses.Should().Contain("@page \"/identity/addresses\"");
        addresses.Should().Contain("OnRowClick=\"@((IdentityAddress item) => OpenAddressDetail(item))\"");
        addresses.Should().Contain("Navigation.NavigateTo($\"/identity/addresses/{address.Id}\")");
        addresses.Should().NotContain("OpenEditDialog");
        addresses.Should().NotContain("Edit Address");

        userDetail.Should().Contain("Navigation.NavigateTo($\"/identity/contacts/{contact.Id}\")");
        userDetail.Should().Contain("Navigation.NavigateTo($\"/identity/addresses/{addr.Id}\")");
        userDetail.Should().NotContain("OpenEditContactDialog");
        userDetail.Should().NotContain("OpenEditAddressDialog");
    }

    [Test]
    public void IdentityServerUserDetail_UsesRouteBackedSidebarSections()
    {
        var repositoryRoot = FindRepositoryRoot();
        var pagesRoot = GetIdentityPagesRoot();
        var layoutRoot = Path.Combine(
            repositoryRoot.FullName,
            "src",
            "Presentation",
            "XFramework.Portal",
            "Components",
            "Layout");

        var userDetail = File.ReadAllText(Path.Combine(pagesRoot, "UserDetail.razor"));
        var mainLayout = File.ReadAllText(Path.Combine(layoutRoot, "MainLayout.razor"));
        var sidebar = File.ReadAllText(Path.Combine(layoutRoot, "UserDetailSidebar.razor"));

        userDetail.Should().Contain("@page \"/identity/users/{Id:guid}/{Section}\"");
        userDetail.Should().Contain("@switch (CurrentSection)");
        userDetail.Should().Contain("NormalizeSection");
        userDetail.Should().NotContain("<BbTabsList");
        userDetail.Should().NotContain("<BbTabsTrigger");
        userDetail.Should().NotContain("<BbTabsContent");

        mainLayout.Should().Contain("TryGetUserDetailRoute(out var userRouteId");
        mainLayout.Should().Contain("<UserDetailSidebar UserId=\"@userRouteId\" />");
        sidebar.Should().Contain("GroupId=\"user-detail\"");
        sidebar.Should().Contain("SectionHref(\"summary\")");
        sidebar.Should().Contain("SectionHref(\"credentials\")");
        sidebar.Should().Contain("SectionHref(\"roles\")");
        sidebar.Should().Contain("SectionHref(\"contacts\")");
        sidebar.Should().Contain("SectionHref(\"addresses\")");
        sidebar.Should().Contain("SectionHref(\"sessions\")");
        sidebar.Should().Contain("SectionHref(\"auth-logs\")");
        sidebar.Should().Contain("SectionHref(\"wallets\")");
    }

    [Test]
    public void IdentityServerSensitiveMutations_UseServiceWrapperPaths()
    {
        var pagesRoot = GetIdentityPagesRoot();
        var userDetail = File.ReadAllText(Path.Combine(pagesRoot, "UserDetail.razor"));
        var credentials = File.ReadAllText(Path.Combine(pagesRoot, "Credentials.razor"));
        var tenants = File.ReadAllText(Path.Combine(pagesRoot, "Tenants.razor"));

        userDetail.Should().Contain("[Inject] private IIdentityServerServiceWrapper IdentityServer");
        userDetail.Should().Contain("IdentityServer.CreateCredential(new CreateCredentialRequest");
        userDetail.Should().Contain("IdentityServer.Logout(new LogoutRequest");
        userDetail.Should().Contain("IdentityServer.UploadCredentialAvatar(new UploadCredentialAvatarRequest");
        userDetail.Should().Contain("IdentityServer.RemoveCredentialAvatar(new RemoveCredentialAvatarRequest");
        userDetail.Should().Contain("IdentityServer.AssignCredentialRole(new AssignCredentialRoleRequest");
        userDetail.Should().Contain("IdentityServer.RemoveCredentialRole(new RemoveCredentialRoleRequest");
        userDetail.Should().Contain("IdentityServer.GetCredentialRolePermissionOverrides(new GetCredentialRolePermissionOverridesRequest");
        userDetail.Should().Contain("IdentityServer.SetCredentialRolePermissionOverrides(new SetCredentialRolePermissionOverridesRequest");
        userDetail.Should().Contain("title=\"Edit role permission overrides\"");
        credentials.Should().Contain("[Inject] private IIdentityServerServiceWrapper IdentityServer");
        credentials.Should().Contain("IdentityServer.UploadCredentialAvatar(new UploadCredentialAvatarRequest");
        credentials.Should().Contain("IdentityServer.RemoveCredentialAvatar(new RemoveCredentialAvatarRequest");
        tenants.Should().Contain("[Inject] private IIdentityServerServiceWrapper IdentityServer");
        tenants.Should().Contain("new CreateTenantRequest");
        tenants.Should().Contain("IdentityServer.CreateTenant(request)");
        tenants.Should().Contain("new DeleteTenantRequest");
        tenants.Should().Contain("IdentityServer.DeleteTenant(new DeleteTenantRequest");
        tenants.Should().NotContain("DataContext.Add(tenant)");
        tenants.Should().NotContain("DataContext.Update(");
        tenants.Should().NotContain("SaveChangesAsync(");
        tenants.Should().NotContain("TenantId = tenantId");
        userDetail.Should().NotContain("BCrypt.Net.BCrypt.HashPassword");
        userDetail.Should().NotContain("DataContext.Add(credential)");
        userDetail.Should().NotContain("DataContext.Add(role)");
        userDetail.Should().NotContain("DataContext.Update(_removingRole)");
        userDetail.Should().NotContain("DataContext.Remove(_removingRole)");
        userDetail.Should().NotContain("DataContext.Update(session)");
        userDetail.Should().NotContain("fresh.Status = CurrentSessionState.Inactive");
    }

    [Test]
    public void IdentityServerRoleCapabilities_UseSharedWrapperBackedPermissionSurfaces()
    {
        var pagesRoot = GetIdentityPagesRoot();
        var tenantDetail = File.ReadAllText(Path.Combine(pagesRoot, "TenantDetail.razor"));
        var roleTypeDetail = File.ReadAllText(Path.Combine(pagesRoot, "RoleTypeDetail.razor"));
        var userDetail = File.ReadAllText(Path.Combine(pagesRoot, "UserDetail.razor"));

        tenantDetail.Should().Contain("/identity/tenants/{Id}/role-types/{rt.Id}");
        tenantDetail.Should().Contain("title=\"Edit role type permissions\"");
        tenantDetail.Should().Contain("<BbDataGrid Items=\"@_detailRoleTypes\" ShowPagination=\"true\" InitialPageSize=\"10\">");
        tenantDetail.Should().Contain("<BbDataGridTemplateColumn Title=\"Role Level\" Sortable=\"true\" Filterable=\"true\" FilterBy=\"@(rt => FormatRoleLevel(rt))\">");
        tenantDetail.Should().Contain("<BbDataGridTemplateColumn Title=\"Group\" Filterable=\"true\" FilterBy=\"@(rt => GetRoleTypeGroupLabel(rt))\">");
        tenantDetail.Should().Contain("<BbDataGridTemplateColumn Title=\"Enabled\" Filterable=\"true\" FilterBy=\"@(rt => FormatBoolean(rt.IsEnabled))\">");
        tenantDetail.Should().Contain("<EmptyTemplate>");
        tenantDetail.Should().Contain("<BbEmpty Title=\"No role types\"");

        roleTypeDetail.Should().Contain("@page \"/identity/tenants/{TenantId:guid}/role-types/{RoleTypeId:guid}\"");
        roleTypeDetail.Should().Contain("<BbTreeView TItem=\"PermissionFeatureTreeNode\"");
        roleTypeDetail.Should().Contain("IdentityServer.GetTenantAuthorizationPolicy(new GetTenantAuthorizationPolicyRequest");
        roleTypeDetail.Should().Contain("IdentityServer.UpdateTenantAuthorizationPolicy(new UpdateTenantAuthorizationPolicyRequest");
        roleTypeDetail.Should().Contain("IdentityServer.GetRoleTypePermissions(new GetRoleTypePermissionsRequest");
        roleTypeDetail.Should().Contain("IdentityServer.SetRoleTypePermissions(new SetRoleTypePermissionsRequest");
        roleTypeDetail.Should().Contain("IdentityAuthorizationConstants.CapabilityKeys");
        roleTypeDetail.Should().Contain("MissingPermissionBehavior.Allow");
        roleTypeDetail.Should().Contain("MissingPermissionBehavior.Deny");
        roleTypeDetail.Should().Contain("_permissionsLoaded");
        roleTypeDetail.Should().Contain("title=\"Back to role types\"");
        roleTypeDetail.Should().Contain(".Where(x => !x.IsDeleted && x.IsEnabled)");
        roleTypeDetail.Should().NotContain("_roleType is null || !_permissionsLoaded");
        roleTypeDetail.Should().NotContain("DataContext.Add(");
        roleTypeDetail.Should().NotContain("DataContext.Update(");
        roleTypeDetail.Should().NotContain("DataContext.Remove(");
        roleTypeDetail.Should().NotContain("SaveChangesAsync(");

        userDetail.Should().Contain("Role Permission Overrides");
        userDetail.Should().Contain("IdentityServer.GetCredentialRolePermissionOverrides(new GetCredentialRolePermissionOverridesRequest");
        userDetail.Should().Contain("IdentityServer.SetCredentialRolePermissionOverrides(new SetCredentialRolePermissionOverridesRequest");
        userDetail.Should().Contain("IdentityAuthorizationConstants.CapabilityKeys");
        userDetail.Should().Contain("RoleCapabilityPermissionEffect.Allow");
        userDetail.Should().Contain("RoleCapabilityPermissionEffect.Deny");
        userDetail.Should().Contain("_rolePermissionOverridesLoaded");
        userDetail.Should().Contain("<BbDialogContent TrapFocus=\"false\"");
        userDetail.Should().Contain("Class=\"identity-role-permissions-dialog\">");
        userDetail.Should().Contain("<div class=\"identity-role-permissions-dialog-body\">");
        userDetail.Should().NotContain("max-h-[65vh]");
        userDetail.Should().Contain("<BbDataGridTemplateColumn Title=\"Level\" Sortable=\"true\" Filterable=\"true\" FilterBy=\"@(role => GetRoleLevelFilter(role))\">");
        userDetail.Should().Contain("<BbDataGridTemplateColumn Title=\"Expiration\" Sortable=\"true\" Filterable=\"true\" FilterBy=\"@(role => GetRoleExpirationFilter(role))\">");
        userDetail.Should().Contain("title=\"Remove assigned role\"");
        userDetail.Should().Contain("<BbFormFieldDatePicker @bind-Value=\"_roleForm.ExpirationDate\" Label=\"Expiration Date\" />");
        userDetail.Should().NotContain("type=\"datetime-local\"");
    }

    [Test]
    public void IdentityServerRolePermissionDialog_LoadsBeforeOpeningAndRendersOnlyTheSelectedFeature()
    {
        var userDetail = File.ReadAllText(Path.Combine(GetIdentityPagesRoot(), "UserDetail.razor"));

        var dialogStart = userDetail.IndexOf(
            "<!-- Credential Role Permission Overrides Dialog -->",
            StringComparison.Ordinal);
        var dialogEnd = userDetail.IndexOf(
            "<!-- Remove Role Confirmation -->",
            dialogStart,
            StringComparison.Ordinal);
        dialogStart.Should().BeGreaterThanOrEqualTo(0);
        dialogEnd.Should().BeGreaterThan(dialogStart);

        var dialogSource = userDetail[dialogStart..dialogEnd];
        dialogSource.Should().Contain("<BbTreeView TItem=\"PermissionFeatureTreeNode\"");
        dialogSource.Should().Contain("SelectedRolePermissionFeature");
        dialogSource.Should().Contain("ShowClose=\"@(!_savingRolePermissions)\"");
        dialogSource.Should().Contain("Disabled=\"@_savingRolePermissions\"");
        dialogSource.Should().NotContain("<CenteredSpinner");
        dialogSource.Split("<BbFormFieldSelect", StringSplitOptions.None)
            .Should().HaveCount(2, "the selected feature should render one capability editor template");

        var openMethodStart = userDetail.IndexOf(
            "private async Task OpenRolePermissionsDialog",
            StringComparison.Ordinal);
        var openMethodEnd = userDetail.IndexOf(
            "private void CloseRolePermissionsDialog",
            openMethodStart,
            StringComparison.Ordinal);
        var openMethod = userDetail[openMethodStart..openMethodEnd];
        var loadIndex = openMethod.IndexOf(
            "await IdentityServer.GetCredentialRolePermissionOverrides",
            StringComparison.Ordinal);
        var loadedIndex = openMethod.IndexOf("_rolePermissionOverridesLoaded = true;", StringComparison.Ordinal);
        var dialogOpenIndex = openMethod.IndexOf("_rolePermissionDialogOpen = true;", StringComparison.Ordinal);

        loadIndex.Should().BeGreaterThanOrEqualTo(0);
        loadedIndex.Should().BeGreaterThan(loadIndex);
        openMethod.Should().Contain("IsRolePermissionLoadCurrent(operationVersion, userId, section, role)");
        dialogOpenIndex.Should().BeGreaterThan(
            loadedIndex,
            "portaled dialog content must be complete before the dialog is registered as open");
        openMethod.Split("_rolePermissionDialogOpen = true;", StringSplitOptions.None)
            .Should().HaveCount(2, "the dialog should have one post-load open transition");

        var saveMethodStart = userDetail.IndexOf(
            "private async Task SaveRolePermissionOverrides",
            StringComparison.Ordinal);
        var saveMethodEnd = userDetail.IndexOf(
            "private List<CapabilityPermissionDto> BuildPermissionOverrides",
            saveMethodStart,
            StringComparison.Ordinal);
        var saveMethod = userDetail[saveMethodStart..saveMethodEnd];
        saveMethod.Should().Contain("var operationVersion = _rolePermissionOperationVersion;");
        saveMethod.Should().Contain("IsRolePermissionSaveCurrent(operationVersion, permissionRole)");
        saveMethod.Should().Contain("_savingRolePermissions = true;");

        userDetail.Should().Contain("InvalidateRolePermissionDialogState();");
        userDetail.Should().Contain("AriaLabel=\"@GetRolePermissionActionAriaLabel(role)\"");
    }

    [Test]
    public void IdentityServerAuthorizationBackend_RequiresEndpointAndServiceLevelAuthorization()
    {
        var repositoryRoot = FindRepositoryRoot();
        var authorizationRoot = Path.Combine(
            repositoryRoot.FullName,
            "src",
            "Modules",
            "XFramework.IdentityServer",
            "IdentityServer.Api",
            "Features",
            "Authorization");
        var endpointFiles = Directory.EnumerateFiles(authorizationRoot, "Endpoint.cs", SearchOption.AllDirectories)
            .ToArray();

        endpointFiles.Should().NotBeEmpty();
        foreach (var endpointFile in endpointFiles)
        {
            var source = File.ReadAllText(endpointFile);
            source.Should().Contain(
                "RequireAuthorization = true",
                $"{Path.GetFileName(Path.GetDirectoryName(endpointFile))} must require HTTP authorization");
            source.Should().Contain("[BoltHandler]");
            source.Should().Contain("HandleHttp(");
            source.Should().Contain("HttpContext httpContext");
            source.Should().Contain("IdentityAuthorizationEndpointMetadata.ApplyHttpContextActor(request.Metadata, httpContext);");
        }

        var endpointMetadata = File.ReadAllText(Path.Combine(
            authorizationRoot,
            "Shared",
            "IdentityAuthorizationEndpointMetadata.cs"));
        endpointMetadata.Should().Contain("metadata.TenantId = ResolveGuidClaim(httpContext.User");
        endpointMetadata.Should().Contain("metadata.CredentialId = ResolveGuidClaim(");
        endpointMetadata.Should().Contain("metadata.ServiceAccessToken = null");

        var serviceSource = File.ReadAllText(Path.Combine(
            repositoryRoot.FullName,
            "src",
            "Modules",
            "XFramework.IdentityServer",
            "IdentityServer.Api",
            "Services",
            "IdentityAuthorizationService.cs"));

        serviceSource.Should().Contain("ITrustedServiceInvocationResolver trustedServiceInvocationResolver");
        serviceSource.Should().Contain("IHttpContextAccessor httpContextAccessor");
        serviceSource.Should().Contain("EnsureCallerCapabilityAsync(");
        serviceSource.Should().Contain("EnsureCanInspectCredentialCapabilitiesAsync(");
        serviceSource.Should().Contain("TryResolveAuthenticatedHttpCredential(");
        serviceSource.Should().Contain("httpContextAccessor.HttpContext?.User");
        serviceSource.Should().Contain("XFrameworkServiceNames.IdentityServer");
        serviceSource.Should().Contain("XFrameworkServiceScopes.IdentityAdmin");
        serviceSource.Should().Contain("metadata.TenantId.Value == targetTenantId");
        serviceSource.Should().Contain("new CapabilityDecision(false, \"NoActiveRole\")");
        serviceSource.Should().Contain("RequiresTenantFeature(moduleKey, subFeatureKey)");
        serviceSource.Should().NotContain("IsCoreIdentityFeature(");

        var authServiceSource = File.ReadAllText(Path.Combine(
            repositoryRoot.FullName,
            "src",
            "Modules",
            "XFramework.IdentityServer",
            "IdentityServer.Api",
            "Services",
            "AuthService.cs"));
        authServiceSource.Should().Contain("TenantModuleFeatureKeys.All");
        authServiceSource.Should().Contain("TenantModuleFeatureKeys.Identity");
        authServiceSource.Should().Contain("new TenantModuleFeature");
    }

    [Test]
    public void IdentityServerAuthorizationRoutes_UseCorrectFeatureGatesAndDisableGeneratedRoleWrites()
    {
        var repositoryRoot = FindRepositoryRoot();
        var routes = File.ReadAllText(Path.Combine(
            repositoryRoot.FullName,
            "src",
            "Modules",
            "XFramework.IdentityServer",
            "IdentityServer.Api",
            "Infrastructure",
            "IdentityServerFeatureGateRoutes.cs"));
        var identityRole = File.ReadAllText(Path.Combine(
            repositoryRoot.FullName,
            "src",
            "Modules",
            "XFramework.IdentityServer",
            "IdentityServer.Domain.Shared",
            "Contracts",
            "IdentityRole.cs"));

        var tenantPolicyRouteIndex = routes.IndexOf(
            "TenantModuleFeatureKeys.IdentityTenants, \"/api/identity/authorization/tenant-policy\"",
            StringComparison.Ordinal);
        var authorizationRouteIndex = routes.IndexOf(
            "TenantModuleFeatureKeys.IdentityRoles, \"/api/identity/authorization\"",
            StringComparison.Ordinal);

        tenantPolicyRouteIndex.Should().BeGreaterThanOrEqualTo(0);
        authorizationRouteIndex.Should().BeGreaterThanOrEqualTo(0);
        tenantPolicyRouteIndex.Should().BeLessThan(authorizationRouteIndex);

        identityRole.Should().Contain("Actions = EndpointActions.Get | EndpointActions.GetList");
        identityRole.Should().NotContain("EndpointActions.Create");
        identityRole.Should().NotContain("EndpointActions.Delete");
    }

    [Test]
    public void IdentityServerRoleCapabilityMigration_BackfillsAdminPermissions()
    {
        var repositoryRoot = FindRepositoryRoot();
        var migration = File.ReadAllText(Path.Combine(
            repositoryRoot.FullName,
            "src",
            "Kernel",
            "XFramework.Domain",
            "Migrations",
            "20260705084341_AddIdentityRoleCapabilities.cs"));

        migration.Should().Contain("\"Identity\".\"TenantAuthorizationPolicy\"");
        migration.Should().Contain("\"Identity\".\"TenantModuleFeature\"");
        migration.Should().Contain("\"Identity\".\"IdentityRoleTypeFeaturePermission\"");
        migration.Should().Contain("('identity', 'roles', 'Identity Roles'");
        migration.Should().Contain("('identity', 'tenants', 'Identity Tenants'");
        migration.Should().Contain("role_type.\"SystemReferenceId\" = '6e7b6bf5-6ad6-49fb-80b0-38e967fc35f3'");
        migration.Should().Contain("('identity', 'roles')");
        migration.Should().Contain("VALUES ('view'), ('create'), ('update'), ('delete'), ('manage')");
    }

    [Test]
    public void IdentityServerCredentialAvatarUi_UsesSafeAvatarDisplayAndNoInlineBlobRendering()
    {
        var pagesRoot = GetIdentityPagesRoot();
        var credentials = File.ReadAllText(Path.Combine(pagesRoot, "Credentials.razor"));
        var userDetail = File.ReadAllText(Path.Combine(pagesRoot, "UserDetail.razor"));
        var source = credentials + Environment.NewLine + userDetail;

        credentials.Should().Contain("<BbAvatar");
        credentials.Should().Contain("<BbAvatarImage Src=\"@item.AvatarUrl\"");
        credentials.Should().Contain("GetCredentialInitials(item)");
        userDetail.Should().Contain("<BbAvatar");
        userDetail.Should().Contain("<BbAvatarImage Src=\"@cred.AvatarUrl\"");
        userDetail.Should().Contain("GetCredentialInitials(cred)");
        source.Should().Contain("<BbFileUpload");
        source.Should().Contain("ShowPreview=\"false\"");
        source.Should().NotContain("Convert.ToBase64String");
        source.Should().NotContain("data:image");
        source.Should().NotContain("PreviewUrl");
    }

    [Test]
    public void IdentityServerPages_DoNotRenderSensitiveTokensOrRawPayloads()
    {
        var pagesRoot = GetIdentityPagesRoot();
        var source = string.Join(
            Environment.NewLine,
            Directory.EnumerateFiles(pagesRoot, "*.razor").Select(File.ReadAllText));

        source.Should().NotContain("Title=\"Token\"");
        source.Should().NotContain("Title=\"Session Data\"");
        source.Should().NotContain("Truncate(item.Token");
        source.Should().NotContain("Truncate(item.SessionData");
    }

    [Test]
    public void IdentityServerRelationshipGrids_RenderLabelsInsteadOfRawGuidColumns()
    {
        var pagesRoot = GetIdentityPagesRoot();
        var source = string.Join(
            Environment.NewLine,
            Directory.EnumerateFiles(pagesRoot, "*.razor").Select(File.ReadAllText));

        source.Should().NotContain("Title=\"Credential ID\"");
        source.Should().NotContain("Title=\"User ID\"");
        source.Should().NotContain("Title=\"Group ID\"");
        source.Should().NotContain("Label=\"Credential ID\"");
        source.Should().NotContain("Label=\"User ID (IdentityInfoId)\"");
        source.Should().NotContain("Label=\"Group ID\"");
        source.Should().NotContain("GUID or blank for root");
    }

    [Test]
    public void IdentityServerPages_UseSemanticSafeToasts()
    {
        var pagesRoot = GetIdentityPagesRoot();
        var source = string.Join(
            Environment.NewLine,
            Directory.EnumerateFiles(pagesRoot, "*.razor").Select(File.ReadAllText));

        source.Should().NotContain("ToastService.Show(");
        source.Should().NotContain("ex.Message");
        source.Should().NotContain("result.Message");
        source.Should().NotContain("r.Message");
        source.Should().Contain("ToastService.Success(");
        source.Should().Contain("ToastService.Error(");
        source.Should().Contain("ToastService.Warning(");
    }

    [Test]
    public void SharedConfirmDeleteDialog_UsesSupportedBlueprintAlertDialogComposition()
    {
        var repositoryRoot = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(
            repositoryRoot.FullName,
            "src",
            "Presentation",
            "XFramework.Portal",
            "Components",
            "Shared",
            "ConfirmDeleteDialog.razor"));

        source.Should().Contain("<BbAlertDialogAction>");
        source.Should().Contain("Variant=\"ButtonVariant.Destructive\"");
        source.Should().NotContain("<BbAlertDialogAction OnClick=");
    }

    [Test]
    public void IdentityServerListGrids_UsePaginationFiltersAndEmptyTemplates()
    {
        var pagesRoot = GetIdentityPagesRoot();
        var gridPages = new[]
        {
            "Addresses.razor",
            "AuthLogs.razor",
            "Contacts.razor",
            "Credentials.razor",
            "Sessions.razor",
            "Tenants.razor",
            "Users.razor",
            "Verifications.razor"
        };

        foreach (var page in gridPages)
        {
            var source = File.ReadAllText(Path.Combine(pagesRoot, page));
            source.Should().Contain("ShowPagination=\"true\"", page);
            source.Should().Contain("InitialPageSize=", page);
            source.Should().Contain("Filterable=\"true\"", page);
            source.Should().Contain("<EmptyTemplate>", page);
            source.Should().Contain("<BbEmpty", page);
        }
    }

    [Test]
    public void IdentityServerPropertyGridColumns_UseNativeFiltering()
    {
        var pagesRoot = GetIdentityPagesRoot();
        foreach (var page in Directory.EnumerateFiles(pagesRoot, "*.razor"))
        {
            var propertyColumns = File.ReadAllLines(page)
                .Where(line => line.Contains("<BbDataGridPropertyColumn", StringComparison.Ordinal))
                .ToArray();
            if (propertyColumns.Length == 0)
            {
                continue;
            }

            propertyColumns
                .Should()
                .OnlyContain(column => column.Contains("Filterable=\"true\""), $"{Path.GetFileName(page)} property columns should opt into native grid filtering");
        }
    }

    [Test]
    public void SharedEntityPickerAdvancedSearch_RemainsMultiColumnAndFilterable()
    {
        var repositoryRoot = FindRepositoryRoot();
        var pickerPath = Path.Combine(
            repositoryRoot.FullName,
            "src",
            "Presentation",
            "XFramework.Portal",
            "Components",
            "Shared",
            "XfEntityPicker.razor");

        var pickerText = File.ReadAllText(pickerPath);

        pickerText.Should().Contain("<BbDataGrid Items=\"@AdvancedFilteredItems\"");
        pickerText.Should().Contain("EffectiveAdvancedColumns");
        pickerText.Should().Contain("AdvancedFilters");
        pickerText.Should().Contain("FilterBy=\"@(item => FormatAdvancedCell(column.ValueSelector(item)))\"");
    }

    private static string GetIdentityPagesRoot()
    {
        var repositoryRoot = FindRepositoryRoot();
        return Path.Combine(
            repositoryRoot.FullName,
            "src",
            "Presentation",
            "XFramework.Portal",
            "Components",
            "Pages",
            "Identity");
    }

    private static DirectoryInfo FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "XFramework.slnx")))
                return current;

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate XFramework repository root.");
    }
}
