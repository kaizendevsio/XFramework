namespace IdentityServer.Domain.Generic.Contracts.Responses.Address;

public class CurrencyEntityResponse
{
    public bool? IsEnabled { get; set; }
    public DateTime? CreatedAt { get; set; }
    public long? CreatedBy { get; set; }
    public DateTime? ModifiedAt { get; set; }
    public long? ModifiedBy { get; set; }
    public string Name { get; set; }
    public string CurrencyIsoCode3 { get; set; }
    public string Description { get; set; }
    public short? CurrencyType { get; set; }
    public string Guid { get; set; }
    
}