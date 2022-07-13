namespace HealthEssentials.Domain.Generics.Contracts.Requests.Logistic.Update;

public class UpdateLogisticRiderTagRequest : RequestBase
{
    public Guid LogisticRiderGuid { get; set; }
    public string? Value { get; set; }
    public Guid? TagGuid { get; set; }
}