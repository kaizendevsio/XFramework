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
   public void ToProfile()
   {
      NavigationManager.NavigateTo("/Profile");
   }
   
   public void ToWallet()
   {
      NavigationManager.NavigateTo("/Wallet");
   }
   public void ToUser()
   {
      NavigationManager.NavigateTo("/User");
   }
   public void ToAdminSettings()
   {
      NavigationManager.NavigateTo("/AdminSettings");
   }
}