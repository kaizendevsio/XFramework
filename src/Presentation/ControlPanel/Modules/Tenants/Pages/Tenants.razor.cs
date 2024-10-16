using System.Runtime.InteropServices;
using IdentityServer.Integration.Drivers;
using Microsoft.AspNetCore.Components;
using Tenant.Integration.Drivers;

namespace ControlPanel.Modules.Tenants.Pages;

public partial class Tenants
{
    [Inject]
    private ITenantServiceWrapper TenantServiceWrapper { get; set; }
    public Tenants()
    {
        View.Title = "Tenants";
    }

    private List<XFramework.Domain.Shared.Contracts.Tenant>? List { get; set; }

    private void ButtonAction()
    {
        
    }
    
    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
        await Task.Delay(TimeSpan.FromSeconds(2));
        var apiResult = await TenantServiceWrapper.Tenant.GetList(pageSize: 100, pageNumber: 0);
        List = apiResult.Response?.Items.ToList();
        Console.WriteLine($"Total Items: {apiResult.Response?.TotalItems}");
    }

    public void ShowDetails()
    {
        NavigationManager.NavigateTo("tenant/details");
    }

}