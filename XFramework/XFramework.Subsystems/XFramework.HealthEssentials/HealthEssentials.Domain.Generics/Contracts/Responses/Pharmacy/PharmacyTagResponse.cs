namespace HealthEssentials.Domain.Generics.Contracts.Responses.Pharmacy;

public class PharmacyTagResponse
{
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
    public bool? IsEnabled { get; set; }
    public bool IsDeleted { get; set; }
    public long PharmacyId { get; set; }
    public string? Value { get; set; }
    public long? TagId { get; set; }
    public string Guid { get; set; } = null!;
}