namespace IdentityServer.Domain.Generic.Contracts.Responses.Address;

public class AddressCityResponse
{
    public long PsgcCode { get; set; }
    public string Description { get; set; }
    public long ProvCode { get; set; }
    public long Code { get; set; }
    public int? RegCode { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string Guid { get; set; }
    
    public List<AddressBarangayResponse> AddressBarangays { get; set; }
}