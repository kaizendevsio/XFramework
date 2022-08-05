namespace HealthEssentials.Domain.Generics.Contracts.Requests.Medicine.Create;

public class CreateMedicineRequest : RequestBase
{
    public Guid? EntityGuid { get; set; }
    public string? Name { get; set; }
    public string? ShortName { get; set; }
    public string? ScientificName { get; set; }
    public string? Description { get; set; }
    public string? ChemicalComponent { get; set; }
}