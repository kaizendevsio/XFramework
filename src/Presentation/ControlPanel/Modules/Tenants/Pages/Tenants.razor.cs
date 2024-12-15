using System.Runtime.InteropServices;
using IdentityServer.Integration.Drivers;
using Microsoft.AspNetCore.Components;
using Tenant.Integration.Drivers;
using XFramework.Domain.Shared.Contracts.Responses;

namespace ControlPanel.Modules.Tenants.Pages;

public partial class Tenants
{
    [Inject]
    private ITenantServiceWrapper TenantServiceWrapper { get; set; }
    public Tenants()
    {
        View.Title = "Tenants";
    }

    private PaginatedResult<XFramework.Domain.Shared.Contracts.Tenant>? List { get; set; }

    private void ButtonAction()
    {
        
    }
    
    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
        var apiResult = await TenantServiceWrapper.Tenant.GetList(pageSize: 100, pageNumber: 0);
        List = apiResult.Response;
    }

    public void ShowDetails()
    {
        NavigationManager.NavigateTo("tenant/details");
    }

}