namespace XFramework.Administrator.Pages.Dashboard;

public class DashboardBase : PageBase
{
  
   public void ToDashboard()
   {
      NavigationManager.NavigateTo("/Dashboard");
   }
   
   public void ToSignIn()
   {
      NavigationManager.NavigateTo("/Session/SignIn");
   }
   
   public void ToSignUp()
   {
      NavigationManager.NavigateTo("/Session/SignUp");
   }
}