namespace HealthEssentials.Domain.Generics.Contracts.Responses.Ailment;

public class AilmentTagResponse
{
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
    public bool? IsEnabled { get; set; }
    public bool IsDeleted { get; set; }
    public long AilmentId { get; set; }
    public string? Value { get; set; }
    public string Guid { get; set; } = null!;
    public long TagId { get; set; }
    
    /*public Ailment Ailment { get; set; } = null!;*/
}