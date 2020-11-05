using Microsoft.AspNetCore.Components;
using Ark.Mobile.Shared.Layouts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Ark.Mobile.Pages.Client.User
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
