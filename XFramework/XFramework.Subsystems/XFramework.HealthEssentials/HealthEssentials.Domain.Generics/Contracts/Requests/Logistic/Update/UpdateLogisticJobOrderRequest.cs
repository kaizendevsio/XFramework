namespace HealthEssentials.Domain.Generics.Contracts.Requests.Logistic.Update;

public class UpdateLogisticJobOrderRequest : RequestBase
{
    public Guid? RiderGuid { get; set; }
    public short Status { get; set; }
    public Guid? ScheduleGuid { get; set; }
}