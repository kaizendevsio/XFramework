using HealthEssentials.Domain.DataTransferObjects.XnelSystemsHealthEssentials;

namespace HealthEssentials.Domain.Generics.Contracts.Responses.Ailment;

public class AilmentResponse
{
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
    public bool? IsEnabled { get; set; }
    public bool IsDeleted { get; set; }
    public long EntityId { get; set; }
    public string? Name { get; set; }
    public string? ShortName { get; set; }
    public string? OtherName { get; set; }
    public string? Description { get; set; }
    public string Guid { get; set; } = null!;
    
    public AilmentEntity Entity { get; set; } = null!;
}