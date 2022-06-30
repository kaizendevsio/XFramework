namespace IdentityServer.Domain.Generic.Contracts.Responses;

public class IdentityRoleEntityResponse
{
    public bool IsEnabled { get; set; }
    public DateTime CreatedAt { get; set; }
    public long? CreatedBy { get; set; }
    public DateTime? ModifiedAt { get; set; }
    public long? ModifiedBy { get; set; }
    public bool IsDeleted { get; set; }
    public string Name { get; set; }
    public short? RoleLevel { get; set; }
    public string Guid { get; set; }
    public long ApplicationId { get; set; }
    public long? GroupId { get; set; }
}