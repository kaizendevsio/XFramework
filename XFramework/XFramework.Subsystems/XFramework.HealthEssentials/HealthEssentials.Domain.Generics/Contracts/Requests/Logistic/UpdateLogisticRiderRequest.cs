using XFramework.Domain.Generic.Contracts.Requests;

namespace HealthEssentials.Domain.Generics.Contracts.Requests.Logistic;

public class UpdateLogisticRiderRequest : RequestBase
{
    public Guid? CredentialGuid { get; set; }
    public string? ProfessionalName { get; set; }
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Description { get; set; }

    public Guid? TypeGuid { get; set; }
}