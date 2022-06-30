namespace IdentityServer.Domain.Generic.Contracts.Responses.Verification;

public class IdentityVerificationResponse
{
    public bool? IsEnabled { get; set; }
    public DateTime? CreatedAt { get; set; }
    public long? CreatedBy { get; set; }
    public DateTime? ModifiedAt { get; set; }
    public long? ModifiedBy { get; set; }
    public bool? IsDeleted { get; set; }
    public long? IdentityCredId { get; set; }
    public long? VerificationTypeId { get; set; }
    public short? Status { get; set; }
    public DateTimeOffset? StatusUpdatedOn { get; set; }
    public string Token { get; set; }
    public DateTime? Expiry { get; set; }
    public string Guid { get; set; }
    
    public virtual IdentityVerificationEntityResponse VerificationType { get; set; }
}