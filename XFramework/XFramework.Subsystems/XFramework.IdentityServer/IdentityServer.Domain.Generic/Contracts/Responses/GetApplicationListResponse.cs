namespace IdentityServer.Domain.Generic.Contracts.Responses;

public class GetApplicationListResponse
{
    public string AppName { get; set; }
    public string Description { get; set; }
    public Guid? Guid { get; set; }
    public short? Status { get; set; }
    public DateTime? Expiration { get; set; }
    public DateTime? AvailabilityDate { get; set; }
    public long? ParentAppId { get; set; }
    public DateTime? CreatedOn { get; set; }
}