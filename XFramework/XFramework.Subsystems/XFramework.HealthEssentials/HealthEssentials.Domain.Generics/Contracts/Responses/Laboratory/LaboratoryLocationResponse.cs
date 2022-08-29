using System.Text.Json.Serialization;
using HealthEssentials.Domain.Generics.Contracts.Responses.Consultation;
using HealthEssentials.Domain.Generics.Contracts.Responses.Storage;
using IdentityServer.Domain.Generic.Contracts.Responses.Address;

namespace HealthEssentials.Domain.Generics.Contracts.Responses.Laboratory;

public class LaboratoryLocationResponse
{
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
    public bool? IsEnabled { get; set; }
    public bool IsDeleted { get; set; }
    [JsonIgnore] public long LaboratoryId { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? UnitNumber { get; set; }
    public string? Street { get; set; }
    public string? Building { get; set; }
    public Guid? BarangayGuid { get; set; }
    public Guid? CityGuid { get; set; }
    public string? Subdivision { get; set; }
    public Guid? RegionGuid { get; set; }
    public bool? MainAddress { get; set; }
    public Guid? ProvinceGuid { get; set; }
    public Guid? CountryGuid { get; set; }
    public Guid? Guid { get; set; }
    public int? Status { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Website { get; set; }
    public string? AlternativePhone { get; set; }

    public LaboratoryResponse? Laboratory { get; set; }
    public List<ConsultationJobOrderLaboratoryResponse>? ConsultationJobOrderLaboratories { get; set; }
    public List<LaboratoryJobOrderResponse>? LaboratoryJobOrders { get; set; }
    public List<LaboratoryLocationTagResponse>? LaboratoryLocationTags { get; set; }
    public List<LaboratoryMemberResponse>? LaboratoryMembers { get; set; }
    public List<LaboratoryServiceResponse>? LaboratoryServices { get; set; }
    
    public List<StorageFileResponse>? Files { get; set; }
    public AddressBarangayResponse? Barangay { get; set; }
    public AddressCityResponse? City { get; set; }
    public AddressRegionResponse? Region { get; set; }
    public AddressProvinceResponse? Province { get; set; }
    public AddressCountryResponse? Country { get; set; }
}