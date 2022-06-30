namespace IdentityServer.Domain.Generic.Contracts.Responses;

public class AuthorizeIdentityResponse
{
    public Guid IdentityGuid { get; set; }
    public Guid CredentialGuid { get; set; }
    public string AccessToken  { get; set; }
    public string RefreshToken  { get; set; }

    public List<IdentityRoleResponse> RoleList { get; set; }
}