namespace XFramework.Client.Shared.Entity.ViewModels.Session.Client
{
    public class SignUpVm
    {
        public string PhoneNumber { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }
        public string UserName { get; set; }
        public string PasswordString { get; set; }
        public string ConfirmPasswordString { get; set; }
        public bool Agree { get; set; }
    }
}