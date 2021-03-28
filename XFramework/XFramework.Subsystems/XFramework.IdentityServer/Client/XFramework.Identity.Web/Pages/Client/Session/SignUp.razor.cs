using XFramework.Identity.Shared.Entity.Properties;
using XFramework.Identity.Shared.Entity.ViewModels.User.Session;

namespace XFramework.Identity.Web.Pages.Client.Session
{
    public class SignUpBase : ActivityBase
    {
        public SignUpBase()
        {
            
        }

        protected SignUpVm SignUpVm { get; set; } = new();
    }
}