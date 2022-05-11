namespace HealthEssentials.Domain.Generics.Contracts.Responses.Hospital;

public class HospitalTagResponse
{
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
    public bool? IsEnabled { get; set; }
    public bool IsDeleted { get; set; }
    public long HospitalId { get; set; }
    public string? Value { get; set; }
    public long? TagId { get; set; }
    public string Guid { get; set; } = null!;
}