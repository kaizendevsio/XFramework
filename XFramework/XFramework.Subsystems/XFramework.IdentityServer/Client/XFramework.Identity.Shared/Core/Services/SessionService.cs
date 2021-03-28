using System;
using System.Threading.Tasks;
using XFramework.Identity.Shared.Entity.ViewModels.User;
using XFramework.Identity.Shared.Entity.ViewModels.User.Session;

namespace XFramework.Identity.Shared.Core.Services
{
    public class SessionService
    {
      
        public async Task SignIn(SignInVm request)
        {
            Console.WriteLine("Hello World");
        }
        
        public async Task SignUp(SignUpVm request)
        {
            Console.WriteLine("Hello World - Sign Up");
        }
    }
}