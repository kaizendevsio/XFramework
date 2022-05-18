

namespace HealthEssentials.Domain.Generics.Contracts.Responses.Logistic;

public class LogisticJobOrderResponse
{
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
    public bool? IsEnabled { get; set; }
    public bool IsDeleted { get; set; }
    public long RiderId { get; set; }
    public string Guid { get; set; } = null!;
    public short Status { get; set; }
    public long ScheduleId { get; set; }
    
    public LogisticRiderResponse Rider { get; set; } = null!;

}