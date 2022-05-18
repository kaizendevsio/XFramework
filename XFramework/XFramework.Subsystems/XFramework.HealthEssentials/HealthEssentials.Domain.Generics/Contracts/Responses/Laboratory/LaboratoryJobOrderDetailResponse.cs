namespace HealthEssentials.Domain.Generics.Contracts.Responses.Laboratory;

public class LaboratoryJobOrderDetailResponse
{
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
    public bool? IsEnabled { get; set; }
    public bool IsDeleted { get; set; }
    public Guid? LaboratoryJobOrderGuid { get; set; }
    public Guid? LaboratoryServiceGuid { get; set; }
    public string? Quantity { get; set; }
    public string? Remarks { get; set; }
    public short? Status { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string Guid { get; set; } = null!;
}