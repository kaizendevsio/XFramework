using Ark.Mobile.Property;
using Ark.Mobile.Shared.Layouts;
using Microsoft.AspNetCore.Components;
using System.Threading.Tasks;

namespace Ark.Mobile.Services
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
