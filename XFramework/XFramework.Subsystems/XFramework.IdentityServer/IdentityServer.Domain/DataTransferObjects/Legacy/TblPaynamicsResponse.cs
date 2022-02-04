namespace IdentityServer.Domain.DataTransferObjects.Legacy;

public partial class TblPaynamicsResponse
{
    public long Id { get; set; }
    public string ResponseCode { get; set; }
    public string ResponseMessage { get; set; }
    public string Description { get; set; }
    public long? Status { get; set; }
}