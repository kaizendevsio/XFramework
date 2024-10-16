namespace Fluid.Modules.Tables.Pages;

public partial class TableList
{
    private CreateEntityModal createEntityModal; // Reference to the modal

    public List<Entity> List { get; set; } = new List<Entity>();

    protected override async Task OnInitializedAsync()
    {
        await LoadData();
    }

    private async Task LoadData()
    {
        List = await DbContext.Entities.ToListAsync();
        await InvokeAsync(StateHasChanged);
    }

    private async Task OpenCreateEntityModal()
    {
        await DialogService.Show<CreateEntityModal>().Result; // Use Show method to open the modal
        await LoadData();
    }
    
    private async Task DeleteEntity(Entity entity)
    {
        var confirm = await DialogService
            .ShowMessageBox("Confirm Delete", "Are you sure you want to delete this entity?", yesText: "Yes", cancelText: "No");
        if (confirm is false or null) return;
        DbContext.Entities.Remove(entity);
        await DbContext.SaveChangesAsync();
        await LoadData(); // Update the list after deletion
    }
}