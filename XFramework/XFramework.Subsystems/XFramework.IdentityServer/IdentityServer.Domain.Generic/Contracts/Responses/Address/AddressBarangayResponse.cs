namespace IdentityServer.Domain.Generic.Contracts.Responses.Address;

public class AddressBarangayResponse
{
    public long Id { get; set; }
    public long? Code { get; set; }
    public string Description { get; set; }
    public long? CityCode { get; set; }
    public int? RegCode { get; set; }
    public int? ProvCode { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string Guid { get; set; }
    
}