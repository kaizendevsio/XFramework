using XFramework.Domain.Generic.Contracts.Requests;

namespace IdentityServer.Domain.Generic.Contracts.Responses.Verification;

public class IdentityVerificationEntityResponse : RequestBase
{
    public bool? IsEnabled { get; set; }
    public DateTime? CreatedAt { get; set; }
    public long? CreatedBy { get; set; }
    public DateTime? ModifiedAt { get; set; }
    public string Name { get; set; }
    public long? DefaultExpiry { get; set; }
    public short? Priority { get; set; }
    public bool? IsDeleted { get; set; }
    public string Guid { get; set; }
}