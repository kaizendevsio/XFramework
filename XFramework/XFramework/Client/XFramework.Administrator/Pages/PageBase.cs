using Microsoft.AspNetCore.Components;

namespace XFramework.Administrator.Pages;

public class PageBase : LayoutComponentBase
{
   [Inject] public NavigationManager NavigationManager { get; set; }
}