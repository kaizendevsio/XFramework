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
        var addresses = File.ReadAllText(Path.Combine(pagesRoot, "Addresses.razor"));
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

        addresses.Should().Contain("<XfEntityPicker TItem=\"IdentityInformation\"");
        addresses.Should().Contain("AdvancedColumns=\"@UserAdvancedColumns\"");
        addresses.Should().Contain("AdvancedFilters=\"@UserAdvancedFilters\"");
        addresses.Should().NotContain("Label=\"User ID (IdentityInfoId)\"");

        tenants.Should().Contain("<XfEntityPicker TItem=\"Tenant\"");
        tenants.Should().Contain("AdvancedColumns=\"@TenantAdvancedColumns\"");
        tenants.Should().Contain("AdvancedFilters=\"@TenantAdvancedFilters\"");
        tenants.Should().Contain("AdvancedSearchScope=\"Searches tenant name, description, status, version, expiration, enabled state, and created date.\"");
        tenants.Should().NotContain("Label=\"Parent Tenant ID\"");

        tenantDetail.Should().Contain("<XfEntityPicker TItem=\"RegistryConfigurationGroup\"");
        tenantDetail.Should().Contain("AdvancedColumns=\"@ConfigurationGroupAdvancedColumns\"");
        tenantDetail.Should().Contain("AdvancedFilters=\"@ConfigurationGroupAdvancedFilters\"");
        tenantDetail.Should().NotContain("<BbFormFieldNativeSelect");
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

        pickerText.Should().Contain("xf-entity-picker-advanced-table");
        pickerText.Should().Contain("ToggleAdvancedSort");
        pickerText.Should().Contain("AdvancedFilters");
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
