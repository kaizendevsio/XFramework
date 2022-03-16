namespace IdentityServer.Domain.Generic.Contracts.Responses.Address;

public class IdentityAddressEntityResponse
{
    public string Name { get; set; }
    public string Guid { get; set; }
    
    public List<IdentityAddressResponse> TblIdentityAddresses { get; set; }
}