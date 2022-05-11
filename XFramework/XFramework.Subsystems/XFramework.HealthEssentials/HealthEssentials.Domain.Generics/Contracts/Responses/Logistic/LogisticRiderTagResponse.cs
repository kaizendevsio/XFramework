namespace HealthEssentials.Domain.Generics.Contracts.Responses.Logistic;

public class LogisticRiderTagResponse
{
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
    public bool? IsEnabled { get; set; }
    public bool IsDeleted { get; set; }
    public long LogisticRiderId { get; set; }
    public string? Value { get; set; }
    public long? TagId { get; set; }
    public string Guid { get; set; } = null!;

}