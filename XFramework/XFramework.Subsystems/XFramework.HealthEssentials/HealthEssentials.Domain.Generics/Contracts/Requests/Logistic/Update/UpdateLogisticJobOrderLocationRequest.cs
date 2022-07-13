namespace HealthEssentials.Domain.Generics.Contracts.Requests.Logistic.Update;

public class UpdateLogisticJobOrderLocationRequest : RequestBase
{
    public Guid LogisticJobOrderGuid { get; set; }
    public short Status { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? UnitNumber { get; set; }
    public string? Street { get; set; }
    public string? Building { get; set; }
    public long? Barangay { get; set; }
    public long? City { get; set; }
    public string? Subdivision { get; set; }
    public long? Region { get; set; }
    public bool? MainAddress { get; set; }
    public long? Province { get; set; }
    public long? Country { get; set; }
    public short Priority { get; set; }
}