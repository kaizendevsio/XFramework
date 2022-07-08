namespace HealthEssentials.Domain.Generics.Contracts.Requests.Availability;

public class CreateAvailabilityRequest : RequestBase
{
    public Guid? EntityGuid { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public short? Status { get; set; }
    public DateTime DateStart { get; set; }
    public DateTime DateEnd { get; set; }
    public DateTime TimeStart { get; set; }
    public DateTime TimeEnd { get; set; }
    public bool? IsAvailable { get; set; }
}