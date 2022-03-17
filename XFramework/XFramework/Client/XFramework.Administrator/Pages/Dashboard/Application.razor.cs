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
    
    private List<string> editEvents = new();
    
}