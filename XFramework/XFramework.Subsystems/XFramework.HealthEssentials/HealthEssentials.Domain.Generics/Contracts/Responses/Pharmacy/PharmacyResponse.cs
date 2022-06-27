using HealthEssentials.Domain.Generics.Contracts.Responses.Storage;

namespace HealthEssentials.Domain.Generics.Contracts.Responses.Pharmacy;

public class PharmacyResponse
{
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
    public bool? IsEnabled { get; set; }
    public bool IsDeleted { get; set; }
    public string? Name { get; set; }
    public string? ShortName { get; set; }
    public string? Slogan { get; set; }
    public string? Description { get; set; }
    public string? ChemicalComponent { get; set; }
    public Guid? Guid { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Website { get; set; }
    public string? Logo { get; set; }
    public int Status { get; set; }
    
    public virtual List<PharmacyLocationResponse>? PharmacyLocations { get; set; }
    public List<PharmacyMemberResponse>? PharmacyMembers { get; set; }

    
    public PharmacyEntityResponse? Entity { get; set; }
}