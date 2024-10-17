using Microsoft.AspNetCore.Components;

namespace Fluid.Modules.Tables.Pages;

public partial class TableColumns
{
    public List<EntityColumn> List { get; set; } = [];
    protected override async Task OnInitializedAsync()
    {
        await LoadData();
    }
    private async Task LoadData()
    {
        List = await DbContext.EntityColumns.ToListAsync();
        await InvokeAsync(StateHasChanged);
    }
    private async Task DeleteColumn(EntityColumn column)
    {
        var confirm = await DialogService
            .ShowMessageBox("Confirm Delete", "Are you sure you want to delete this column?", yesText: "Yes", cancelText: "No");
        if (confirm is false or null) return;
        DbContext.EntityColumns.Remove(column);
        await DbContext.SaveChangesAsync();
        await LoadData(); // Update the list after deletion
    }
    private async Task OpenCreateColumnModal()
    {
        await DialogService.Show<CreateEntityColumnModal>().Result; // Use Show method to open the modal
        await LoadData();
    }
}