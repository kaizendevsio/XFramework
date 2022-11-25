

using HealthEssentials.Domain.Generics.Contracts.Responses.Pharmacy;
using HealthEssentials.Domain.Generics.Contracts.Responses.Schedule;
using IdentityServer.Domain.Generic.Contracts.Responses;

namespace HealthEssentials.Domain.Generics.Contracts.Responses.Logistic;

public class LogisticJobOrderResponse
{
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
    public bool? IsEnabled { get; set; }
    public bool IsDeleted { get; set; }
    public Guid? Guid { get; set; }
    public short Status { get; set; }
    
    public LogisticRiderResponse? Rider { get; set; }
    public ScheduleResponse? Schedule { get; set; }
    public virtual List<LogisticJobOrderDetailResponse>? LogisticJobOrderDetails { get; set; }
    public virtual List<LogisticJobOrderLocationResponse>? LogisticJobOrderLocations { get; set; }

}