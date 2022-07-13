using XFramework.Domain.Generic.Contracts.Requests;

namespace HealthEssentials.Domain.Generics.Contracts.Requests.Pharmacy.Update;

public class UpdatePharmacyMemberRequest : RequestBase
{
    public Guid? PharmacyGuid { get; set; }
    public Guid? CredentialGuid { get; set; }
    
    public bool? IsEnabled { get; set; }
    public bool IsDeleted { get; set; }
    public string? Value { get; set; }
    public string? Name { get; set; }
    public GenericStatusType Status { get; set; }
}