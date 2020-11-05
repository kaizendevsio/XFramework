using Ark.Mobile.Property;
using Ark.Mobile.Services;
using Ark.Mobile.Shared.Menus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Ark.Mobile.Pages.Client.Shop
{
    public class HomeBase : ActivityBase
    {
        public HomeBase() {
            ActivityTitle = "Ark";
            ActivityMenuProperty.IsBackEnabled = false;
            

        }
    }
}
