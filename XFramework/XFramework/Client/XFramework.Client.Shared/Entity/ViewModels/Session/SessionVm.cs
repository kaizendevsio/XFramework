namespace XFramework.Client.Shared.Entity.ViewModels.Session
{
    public class SessionVm
    {
        public Guid? Uid { get; set; }
        public Guid? Cuid { get; set; }
        public string AccessToken  { get; set; }
        public string RefreshToken  { get; set; }
    }
}