using MudBlazor;
using MudBlazor.Examples.Data.Models;

namespace XFramework.Administrator.Pages.Dashboard;

public class ApplicationBase : PageBase
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
    
    private CategoryTypes.Element elementBeforeEdit;
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
            Sign = ((CategoryTypes.Element)element).Sign,
            Name = ((CategoryTypes.Element)element).Name,
            Molar = ((CategoryTypes.Element)element).Molar,
            Position = ((CategoryTypes.Element)element).Position
        };
        AddEditionEvent($"RowEditPreview event: made a backup of Element {((CategoryTypes.Element)element).Name}");
    }
    
    public void ItemHasBeenCommitted(object element)
    {
        AddEditionEvent($"RowEditCommit event: Changes to Element {((CategoryTypes.Element)element).Name} committed");
    }

    public void ResetItemToOriginalValues(object element)
    {
        ((CategoryTypes.Element)element).Sign = elementBeforeEdit.Sign;
        ((CategoryTypes.Element)element).Name = elementBeforeEdit.Name;
        ((CategoryTypes.Element)element).Molar = elementBeforeEdit.Molar;
        ((CategoryTypes.Element)element).Position = elementBeforeEdit.Position;
        AddEditionEvent($"RowEditCancel event: Editing of Element {((CategoryTypes.Element)element).Name} cancelled");
    }

    public bool FilterFunc(CategoryTypes.Element element)
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
    
    public TableGroupDefinition<CategoryTypes.Element> _groupDefinition = new()
    {
        GroupName = "Group",
        Indentation = false,
        Expandable = false,
        Selector = (e) => e.Group
    };
}