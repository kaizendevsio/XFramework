using MudBlazor;
using MudBlazor.Examples.Data.Models;

namespace XFramework.Administrator.Pages.Dashboard;

public class WalletTypesBase : PageBase
{
     public bool dense = false;
    public bool hover = true;
    public string searchString1 = "";
    public bool sort = true;
    public bool multiSelect = true;
    public bool pagination;
    public bool ronly = false;
    public bool bordered = false;
    private string searchString = "";
    
    private Element elementBeforeEdit;
    private List<string> editEvents = new();
    
    private void AddEditionEvent(string message)
    {
        editEvents.Add(message);
        StateHasChanged();
    }

    public void BackupItem(object element)
    {
        elementBeforeEdit = new()
        {
            Sign = ((Element)element).Sign,
            Name = ((Element)element).Name,
            Molar = ((Element)element).Molar,
            Position = ((Element)element).Position
        };
        AddEditionEvent($"RowEditPreview event: made a backup of Element {((Element)element).Name}");
    }
    
    public void ItemHasBeenCommitted(object element)
    {
        AddEditionEvent($"RowEditCommit event: Changes to Element {((Element)element).Name} committed");
    }

    public void ResetItemToOriginalValues(object element)
    {
        ((Element)element).Sign = elementBeforeEdit.Sign;
        ((Element)element).Name = elementBeforeEdit.Name;
        ((Element)element).Molar = elementBeforeEdit.Molar;
        ((Element)element).Position = elementBeforeEdit.Position;
        AddEditionEvent($"RowEditCancel event: Editing of Element {((Element)element).Name} cancelled");
    }

    public bool FilterFunc(Element element)
    {
        if (string.IsNullOrWhiteSpace(searchString))
            return true;
        if (element.Sign.Contains(searchString, StringComparison.OrdinalIgnoreCase))
            return true;
        if (element.Name.Contains(searchString, StringComparison.OrdinalIgnoreCase))
            return true;
        if ($"{element.Number} {element.Position} {element.Molar}".Contains(searchString))
            return true;
        return false;
    } 
    
    public TableGroupDefinition<Element> _groupDefinition = new()
    {
        GroupName = "Group",
        Indentation = false,
        Expandable = false,
        Selector = (e) => e.Group
    };
}