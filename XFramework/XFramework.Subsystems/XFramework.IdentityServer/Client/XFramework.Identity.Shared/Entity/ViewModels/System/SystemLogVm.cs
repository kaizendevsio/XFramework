namespace XFramework.Identity.Shared.Entity.ViewModels.System
{
    public class SystemLogVm
    {
        public string ID { get; set; }
        public string Initiator { get; set; }
        public string Severity { get; set; }
        public string Message { get; set; }
        public string CreatedAt { get; set; }
        public string ModifedAt { get; set; }
    }
}