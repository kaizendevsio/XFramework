namespace IdentityServer.Domain.Generic.Contracts.Responses;

public class IdentityCredentialResponse
{
    public Guid? Guid { get; set; }
    public bool IsEnabled { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsDeleted { get; set; }
    public long? IdentityInfoId { get; set; }
    public string UserName { get; set; }
    public string UserAlias { get; set; }
    public short? LogInStatus { get; set; }
    public long ApplicationId { get; set; }
    public string Token { get; set; }

    public virtual IdentityInfoResponse IdentityInfo { get; set; }
    public virtual List<IdentityRoleResponse> TblIdentityRoles { get; set; }
    public virtual List<IdentityContactResponse> TblIdentityContacts { get; set; }
}