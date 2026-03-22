using System.Runtime.InteropServices;
using IdentityServer.Integration.Drivers;
using Microsoft.AspNetCore.Components;
// Tenant wrapper is now part of IIdentityServerServiceWrapper
using XFramework.Domain.Shared.Contracts.Responses;
using IdentityServer.Domain.Shared.Contracts;

namespace ControlPanel.Modules.Tenants.Pages;

public partial class Tenants
{
    [Inject]
    private IIdentityServerServiceWrapper IdentityServerServiceWrapper { get; set; }
    public Tenants()
    {
        View.Title = "Tenants";
    }

    private PaginatedResult<IdentityServer.Domain.Shared.Contracts.Tenant>? List { get; set; }

    private void ButtonAction()
    {
        
    }
    
    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
        var apiResult = await IdentityServerServiceWrapper.Tenant.GetList(pageSize: 100, pageNumber: 0);
        List = apiResult.Response;
    }

    public void ShowDetails()
    {
        NavigationManager.NavigateTo("tenant/details");
    }

}