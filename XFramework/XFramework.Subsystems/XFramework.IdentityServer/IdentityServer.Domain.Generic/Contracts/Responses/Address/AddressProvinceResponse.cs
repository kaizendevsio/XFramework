namespace IdentityServer.Domain.Generic.Contracts.Responses.Address;

public class AddressProvinceResponse
{
    public long PsgcCode { get; set; }
    public string Description { get; set; }
    public long RegCode { get; set; }
    public long Code { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string Guid { get; set; }
    
    public List<AddressCityResponse> TblAddressCities { get; set; }
}