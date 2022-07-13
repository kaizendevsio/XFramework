namespace HealthEssentials.Domain.Generics.Contracts.Requests.Logistic.Update;

public class UpdateLogisticEntityRequest : RequestBase
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public int? SortOrder { get; set; }
}