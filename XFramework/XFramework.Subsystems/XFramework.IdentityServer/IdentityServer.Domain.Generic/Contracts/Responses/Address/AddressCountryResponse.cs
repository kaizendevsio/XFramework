namespace IdentityServer.Domain.Generic.Contracts.Responses.Address;

public class AddressCountryResponse
{
    public bool? IsEnabled { get; set; }
    public DateTime? CreatedOn { get; set; }
    public long? CreatedBy { get; set; }
    public DateTime? ModifiedOn { get; set; }
    public long? ModifiedBy { get; set; }
    public DateTime? LastChanged { get; set; }
    public string IsoCode2 { get; set; }
    public string IsoCode3 { get; set; }
    public string Name { get; set; }
    public string Language { get; set; }
    public string PhoneCountryCode { get; set; }
    public long? CurrencyId { get; set; }
    public string Guid { get; set; }

    public CurrencyEntityResponse Currency { get; set; }
    public List<AddressRegionResponse> TblAddressRegions { get; set; }
}