namespace XFramework.Administrator.Pages.Dashboard;

public class DashboardBase : PageBase
{
   public bool open = false;

   public void ToggleDrawer()
   {
      open = !open;
   }
   public void ToDashboard()
   {
      NavigationManager.NavigateTo("/Dashboard");
   }
   
   public void ToSignIn()
   {
      NavigationManager.NavigateTo("/SignIn");
   }
   
   public void ToSignUp()
   {
      NavigationManager.NavigateTo("/SignUp");
   }
}