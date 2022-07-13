namespace HealthEssentials.Domain.Generics.Contracts.Requests.Logistic.Create;

public class CreateLogisticEntityRequest : RequestBase
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public int? SortOrder { get; set; }
}