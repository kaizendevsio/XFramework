using XFramework.Identity.Shared.Entity.Properties;
using XFramework.Identity.Shared.Entity.ViewModels.User.Session;

namespace XFramework.Identity.Web.Pages.Client.Session
{
    public class SignInBase : ActivityBase
    {
        public SignInBase()
        {
            
        }

        protected SignInVm SignInVm { get; set; } = new ();
    }
}