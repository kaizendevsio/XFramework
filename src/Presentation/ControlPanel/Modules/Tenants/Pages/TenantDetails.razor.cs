using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Tenant.Integration.Drivers;
using XFramework.Blazor.Core.Features.Tenant;

namespace ControlPanel.Modules.Tenants.Pages;

public partial class TenantDetails
{
    [Inject] 
    public ITenantServiceWrapper TenantServiceWrapper { get; set; } = null!;
    [Parameter] 
    public Guid TenantId { get; set; }

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
        var tenant = await TenantServiceWrapper.Tenant.Get(TenantId);
        await Mediator.Send(new TenantState.SetState
        {
            SelectedTenant = tenant.Response
        });
    }

    private async Task Save(EditContext arg)
    {
        var tenant = await TenantServiceWrapper.Tenant.Patch(TenantState.SelectedTenant);
        await Mediator.Send(new TenantState.SetState
        {
            SelectedTenant = tenant.Response
        });
    }
}