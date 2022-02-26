
using Microsoft.AspNetCore.SignalR.Client;

namespace XFramework.Integration.Interfaces.Wrappers
{
    public interface IIdentityServiceWrapper : IXFrameworkService
    {
        public HubConnectionState ConnectionState { get; }
        
        public Task<QueryResponseBO<AuthorizeIdentityResponse>> AuthenticateCredential(AuthenticateCredentialRequest request);
        public Task<QueryResponseBO<CredentialResponse>> GetCredential(GetCredentialRequest request);
        public Task<QueryResponseBO<List<CredentialResponse>>> LegacyGetCredentialList(GetCredentialListRequest request);
        public Task<QueryResponseBO<List<CredentialResponse>>> GetCredentialList(GetCredentialListRequest request);
        public Task<QueryResponseBO<ExistenceResponse>> CheckCredentialExistence(CheckCredentialExistenceRequest request);
        public Task<CmdResponseBO> CreateCredential(CreateCredentialRequest request);
        public Task<CmdResponseBO> UpdateCredential(UpdateCredentialRequest request);
        public Task<CmdResponseBO> DeleteCredential(DeleteCredentialRequest request);
        public Task<CmdResponseBO> ChangePassword(UpdatePasswordRequest request);
        public Task<CmdResponseBO> SendOneTimePassword(CheckOneTimePasswordRequest request);
        
        public Task<QueryResponseBO<RoleResponse>> GetRole(GetRoleRequest request);
        public Task<QueryResponseBO<List<RoleResponse>>> GetRoleList(GetRoleListRequest request);
        public Task<QueryResponseBO<RoleEntityResponse>> GetRoleEntity(GetRoleEntityRequest request);
        public Task<QueryResponseBO<List<RoleEntityResponse>>> GetRoleEntityList(GetRoleEntityListRequest request);
        
        public Task<QueryResponseBO<IdentityResponse>> GetIdentity(GetIdentityRequest request);
        public Task<QueryResponseBO<ExistenceResponse>> CheckIdentityExistence(CheckIdentityExistenceRequest request);
        public Task<CmdResponseBO> CreateIdentity(CreateIdentityRequest request);
        public Task<CmdResponseBO> UpdateIdentity(UpdateIdentityRequest request);
        public Task<CmdResponseBO> DeleteIdentity(DeleteIdentityRequest request);
        
        public Task<QueryResponseBO<ContactResponse>> GetContact(GetContactRequest request);
        public Task<QueryResponseBO<List<ContactResponse>>> GetContactList(GetContactListRequest request);
        public Task<QueryResponseBO<ExistenceResponse>> CheckContactExistence(CheckContactExistenceRequest request);
        public Task<CmdResponseBO> CreateContact(CreateContactRequest request);
        public Task<CmdResponseBO> UpdateContact(UpdateContactRequest request);
        public Task<CmdResponseBO> DeleteContact(DeleteContactRequest request);
    }
}