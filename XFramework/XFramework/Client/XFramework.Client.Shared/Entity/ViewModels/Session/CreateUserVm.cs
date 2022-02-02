namespace XFramework.Client.Shared.Entity.ViewModels.Session.Client
{
    public class CreateUserVm
    {
        public int ID { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public DateTime Birthdate { get; set; }
        public string Username { get; set; }
        public string Email { get; set; }
        public string PackageName { get; set; }
    }
}