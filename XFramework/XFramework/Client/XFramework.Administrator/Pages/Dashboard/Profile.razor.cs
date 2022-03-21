namespace XFramework.Administrator.Pages.Dashboard;

public class ProfileBase : PageBase
{
    public bool dense = false;
    public bool sort = true;
    public bool multiSelect = true;
    public bool pagination;
    public bool bordered = false;
    private string searchString = "";
}