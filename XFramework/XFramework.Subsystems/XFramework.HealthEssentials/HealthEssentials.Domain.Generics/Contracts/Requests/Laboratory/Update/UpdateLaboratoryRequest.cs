using XFramework.Domain.Generic.Contracts.Requests;

namespace HealthEssentials.Domain.Generics.Contracts.Requests.Laboratory.Update;

public class UpdateLaboratoryRequest : RequestBase
{
    public string? Name { get; set; }
    public string? ShortName { get; set; }
    public string? Description { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Website { get; set; }
    public string? Logo { get; set; }
    public GenericStatusType Status { get; set; }
}