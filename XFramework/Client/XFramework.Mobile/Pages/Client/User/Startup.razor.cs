using System;
using Microsoft.AspNetCore.Components;
using XFramework.Mobile.Shared.Layouts;

namespace XFramework.Mobile.Pages.Client.User
{
    public class StartupBase : ComponentBase
    {
        [CascadingParameter]
        public MainLayout CMainLayout { get; set; }

        protected void ToggleSheetActivity(bool value) {
            Console.WriteLine("InvokeSheetActivity");
            CMainLayout.InvokeSheetActivity(value);
        }
    }
}
