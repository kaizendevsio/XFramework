using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using XFramework.Mobile.Property;
using XFramework.Mobile.Shared.Layouts;

namespace XFramework.Mobile.Services
{
    public class ActivityBase : ComponentBase
    {
        public string ActivityTitle { get; set; }
        public string ActivityQuote { get; set; }
        public ActivityMenuProperty ActivityMenuProperty { get; set; } = new ActivityMenuProperty() { IsBackEnabled = true };

        [CascadingParameter]
        public ActivityLayout CActivityLayout { get; set; }

        protected override async Task OnInitializedAsync()
        {
            ActivityMenuProperty = ActivityMenuProperty;

            CActivityLayout.UpdateActivityTitle(ActivityTitle);
            CActivityLayout.UpdateActivityMenu(ActivityMenuProperty);
        }
    }
}
