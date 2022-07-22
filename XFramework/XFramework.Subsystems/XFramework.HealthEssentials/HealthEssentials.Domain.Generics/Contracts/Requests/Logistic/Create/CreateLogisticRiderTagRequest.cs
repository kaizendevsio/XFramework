namespace HealthEssentials.Domain.Generics.Contracts.Requests.Logistic.Create;

public class CreateLogisticRiderTagRequest : RequestBase
{
    public Guid? LogisticRiderGuid { get; set; }
    public string? Value { get; set; }
    public Guid? TagGuid { get; set; }
}