using FluentAssertions;

namespace ControlPanel.E2ETests;

[TestFixture]
[Category("Kind:Integration")]
[Category("Module:IdentityServer")]
[Category("Area:ControlPanelContract")]
public sealed class IdentityServerControlPanelContractTests
{
    [Test]
    public void IdentityServerEntityRelationshipDialogs_UseSharedEntityPickerAdvancedSearch()
    {
        var repositoryRoot = FindRepositoryRoot();
        var pagesRoot = Path.Combine(
            repositoryRoot.FullName,
            "src",
            "Presentation",
            "ControlPanel.Server",
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
            "ControlPanel.Server",
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
        credentials.Should().Contain("[Inject] private IIdentityServerServiceWrapper IdentityServer");
        credentials.Should().Contain("IdentityServer.UploadCredentialAvatar(new UploadCredentialAvatarRequest");
        credentials.Should().Contain("IdentityServer.RemoveCredentialAvatar(new RemoveCredentialAvatarRequest");
        tenants.Should().Contain("[Inject] private IIdentityServerServiceWrapper IdentityServer");
        tenants.Should().Contain("new CreateTenantRequest");
        tenants.Should().Contain("IdentityServer.CreateTenant(request)");
        tenants.Should().NotContain("DataContext.Add(tenant)");
        tenants.Should().NotContain("TenantId = tenantId");
        userDetail.Should().NotContain("BCrypt.Net.BCrypt.HashPassword");
        userDetail.Should().NotContain("DataContext.Add(credential)");
        userDetail.Should().NotContain("DataContext.Update(session)");
        userDetail.Should().NotContain("fresh.Status = CurrentSessionState.Inactive");
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
            "ControlPanel.Server",
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
            "ControlPanel.Server",
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
