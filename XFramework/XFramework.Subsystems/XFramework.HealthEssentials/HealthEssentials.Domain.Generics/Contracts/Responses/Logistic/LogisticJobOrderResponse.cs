

using HealthEssentials.Domain.Generics.Contracts.Responses.Schedule;

namespace HealthEssentials.Domain.Generics.Contracts.Responses.Logistic;

public class LogisticJobOrderResponse
{
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
    public bool? IsEnabled { get; set; }
    public bool IsDeleted { get; set; }
    public string Guid { get; set; } = null!;
    public short Status { get; set; }
    
    public LogisticRiderResponse Rider { get; set; } = null!;
    public ScheduleResponse? Schedule { get; set; }

}