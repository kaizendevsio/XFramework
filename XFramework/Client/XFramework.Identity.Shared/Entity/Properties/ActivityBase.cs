using Microsoft.AspNetCore.Components;
using XFramework.Identity.Shared.Core.Services;

namespace XFramework.Identity.Shared.Entity.Properties
{
    public class ActivityBase: ComponentBase
    {
        [Inject]
        public SessionService SessionService { get; set; }
    }
}