using Bogus;
using ControlPanel.Modules.Identity.Components.Modals;
using ControlPanel.Modules.Identity.ViewModels;
using IdentityServer.Integration.Drivers;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using Tenant.Integration.Drivers;
using XFramework.Domain.Shared.Contracts;
using XFramework.Domain.Shared.Contracts.Responses;
using XFramework.Domain.Shared.Enums;

namespace ControlPanel.Modules.Identity.Pages;

public partial class Users
{
    [Inject]
    private IIdentityServerServiceWrapper IdentityServerServiceWrapper { get; set; }
    public PaginatedResult<UserVm> List { get; set; }
    
    public Users()
    {
        View.Title = "Users";
    }

    protected override async Task OnInitializedAsync()
    {
        var apiResult = await IdentityServerServiceWrapper.IdentityCredential.GetList(
            pageSize: 100,
            pageNumber: 0,
            tenantId: TenantState.SelectedTenant?.Id,
            /*filter:
            [
                new()
                {
                    PropertyName = nameof(IdentityCredential.TenantId),
                    Operation = QueryFilterOperation.Equal,
                    Value = TenantState.SelectedTenant?.Id,
                }
            ],*/
            includeNavigations: true,
            includes: [
                nameof(IdentityCredential.IdentityInfo),
                $"{nameof(IdentityCredential.IdentityRoles)}.{nameof(IdentityRole.Type)}",
                $"{nameof(IdentityCredential.IdentityContacts)}.{nameof(IdentityContact.Type)}"
            ]
            );
        Console.WriteLine(apiResult.Response?.TotalItems);
        List = new()
        {
            TotalItems = apiResult.Response!.TotalItems,
            PageIndex = apiResult.Response!.PageIndex,
            PageSize = apiResult.Response!.PageSize,
            Items = apiResult.Response!.Items.Select(i => new UserVm
            {
                UserId = i.Id,
                UserName = i.UserName!,
                Email = EmailAddress(i),
                Status = i.StatusMessage!,
                Role = string.Join(", " , i.IdentityRoles.Select(x => x.Type?.Name)),
                LastLogin = i.LastSeen,
                CreatedAt = i.CreatedAt,
                IsEnabled = i.IsEnabled
            })
        };
        StateHasChanged();
        await base.OnInitializedAsync();
    }

    private Task ButtonAction()
    {
        var options = new DialogOptions { CloseOnEscapeKey = true };
        return DialogService.ShowAsync<AddUser>("Add User", options);
    }
    private void ShowDetails()
    {
        NavigationManager.NavigateTo("/identity/users/details");
    }
}
