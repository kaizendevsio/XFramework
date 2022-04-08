namespace IdentityServer.Domain.Generic.Contracts.Responses.Address;

public class AddressRegionResponse
{
    public int PsgcCode { get; set; }
    public string Description { get; set; }
    public long Code { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string Guid { get; set; }
    public long? CountryId { get; set; }
    
    public List<AddressProvinceResponse> TblAddressProvinces { get; set; }
}