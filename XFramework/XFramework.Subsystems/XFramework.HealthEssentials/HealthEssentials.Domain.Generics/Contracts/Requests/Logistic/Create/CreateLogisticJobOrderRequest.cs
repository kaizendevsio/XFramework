namespace HealthEssentials.Domain.Generics.Contracts.Requests.Logistic.Create;

public class CreateLogisticJobOrderRequest : RequestBase
{
    public Guid RiderGuid { get; set; }
    public short Status { get; set; }
    public Guid ScheduleGuid { get; set; }
}