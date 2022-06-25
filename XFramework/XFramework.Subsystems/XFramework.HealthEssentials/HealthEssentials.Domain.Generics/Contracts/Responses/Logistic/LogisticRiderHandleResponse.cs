namespace HealthEssentials.Domain.Generics.Contracts.Responses.Logistic;

public class LogisticRiderHandleResponse
{
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
    public bool? IsEnabled { get; set; }
    public bool IsDeleted { get; set; }
    public short Status { get; set; }
    public Guid? Guid { get; set; }

    public LogisticRiderResponse? LogisticRider { get; set; }
    public LogisticResponse? Logistic { get; set; }

}