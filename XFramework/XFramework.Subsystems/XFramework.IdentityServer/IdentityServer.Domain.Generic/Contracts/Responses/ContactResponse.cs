namespace IdentityServer.Domain.Generic.Contracts.Responses;

public class ContactResponse
{
    public Guid? Guid { get; set; }
    public bool IsEnabled { get; set; }
    public DateTime CreatedAt { get; set; }
    public long? CreatedBy { get; set; }
    public DateTime? ModifiedAt { get; set; }
    public long? ModifiedBy { get; set; }
    public bool IsDeleted { get; set; }
    public string Value { get; set; }
    public Guid? CredentialGuid { get; set; }

    public virtual ContactEntityResponse Entity { get; set; }
    public virtual IdentityContactGroupResponse Group { get; set; }
    
}