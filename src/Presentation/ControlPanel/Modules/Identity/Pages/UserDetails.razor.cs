using Bogus;
using ControlPanel.Modules.Identity.ViewModels;
using Microsoft.AspNetCore.Components;

namespace ControlPanel.Modules.Identity.Pages;

public partial class UserDetails
{
    public List<UserVm> List { get; set; } = [];
    
    public UserDetails()
    {
        View.Title = "User Details";
    }

    private void GoBack()
    {
        NavigationManager.NavigateTo("/identity/users");
    }

    [Parameter] public int UserId { get; set; }
    private UserVm user;
    
    protected override async Task OnInitializedAsync()
    {
     
    }

    private async Task SaveChanges()
    {
    
    }

    private async Task DeleteUser()
    {
        bool? confirmed = await DialogService.ShowMessageBox(
            "Delete User",
            "Are you sure you want to delete this user?",
            yesText: "Delete", noText: "Cancel");
        
    }

    private void Cancel()
    {
        NavigationManager.NavigateTo("/identity/users");
    }

}
