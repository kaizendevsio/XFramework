using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using XFramework.Mobile.Property;

namespace XFramework.Mobile.Shared.Layouts
{
    public class ActivityLayoutBase : LayoutComponentBase
    {
        public string ActivityHeaderTitle { get; set; }
        public string ActivityQuote { get; set; }
        public ActivityMenuProperty ActivityMenu { get; set; }
        public ActivityBottomBarProperty ActivityBottomBar { get; set; }

        public void UpdateActivityTitle(string newTitle)
        {
            ActivityHeaderTitle = newTitle;
            StateHasChanged();
        }

        public void UpdateActivityMenu(ActivityMenuProperty _ActivityMenu)
        {
            ActivityMenu = _ActivityMenu;
            StateHasChanged();
        }

        public void UpdateActivityMenu(ActivityBottomBarProperty _ActivityBottomBar)
        {
            ActivityBottomBar = _ActivityBottomBar;
            StateHasChanged();
        }

        protected override async Task OnInitializedAsync()
        {
          
        }
    }
}
