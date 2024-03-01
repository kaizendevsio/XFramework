using System.ComponentModel.DataAnnotations;

namespace XFramework.Domain.Generic.BusinessObjects;

public class ApiStatus
{
    [Key]
    public Guid ResponseId { get; set; } = Guid.NewGuid();
    public string ApplicationName { get; set; }
    public string Status { get; set; }
    public DateTime StartupTime { get; set; }
    public string Environment { get; set; }
    public Host Host { get; set; }
}