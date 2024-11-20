namespace XFramework.Blazor.Core.Features.Tenant;

public partial class TenantState : State<TenantState>
{
    public override void Initialize()
    {
    }

    public Domain.Shared.Contracts.Tenant? SelectedTenant { get; set; }
}