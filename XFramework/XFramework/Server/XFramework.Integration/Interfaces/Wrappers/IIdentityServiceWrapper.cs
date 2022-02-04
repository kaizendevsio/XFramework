
namespace XFramework.Integration.Interfaces.Wrappers
{
    public interface IIdentityServiceWrapper : IXFrameworkService
    {
        public Task<QueryResponseBO<AuthorizeIdentityResponse>> AuthenticateCredential(AuthenticateCredentialRequest request);
        public Task<QueryResponseBO<IdentityCredentialResponse>> GetCredential(GetCredentialRequest request);
        public Task<QueryResponseBO<List<IdentityCredentialResponse>>> GetCredentialList(GetIdentityCredentialListRequest request);
        public Task<QueryResponseBO<ExistenceResponse>> CheckCredentialExistence(CheckCredentialExistenceRequest request);
        public Task<CmdResponseBO> CreateCredential(CreateCredentialRequest request);
        public Task<CmdResponseBO> UpdateCredential(UpdateCredentialRequest request);
        public Task<CmdResponseBO> DeleteCredential(DeleteCredentialRequest request);
        public Task<CmdResponseBO> ChangePassword(UpdatePasswordRequest request);
        
        public Task<QueryResponseBO<List<IdentityCredentialResponse>>> GetIdentityRoleList(GetIdentityRoleListRequest request);
        public Task<QueryResponseBO<List<IdentityRoleEntityResponse>>> GetRoleEntityList(GetRoleEntityListRequest request);
        
        public Task<QueryResponseBO<IdentityInfoResponse>> GetIdentity(GetIdentityRequest request);
        public Task<QueryResponseBO<ExistenceResponse>> CheckIdentityExistence(CheckIdentityExistenceRequest request);
        public Task<CmdResponseBO> CreateIdentity(CreateIdentityRequest request);
        public Task<CmdResponseBO> UpdateIdentity(UpdateIdentityRequest request);
        public Task<CmdResponseBO> DeleteIdentity(DeleteIdentityRequest request);
        
        public Task<QueryResponseBO<IdentityContactResponse>> GetContact(GetContactRequest request);
        public Task<QueryResponseBO<ExistenceResponse>> CheckContactExistence(CheckContactExistenceRequest request);
        public Task<CmdResponseBO> CreateContact(CreateContactRequest request);
        public Task<CmdResponseBO> UpdateContact(UpdateContactRequest request);
        public Task<CmdResponseBO> DeleteContact(DeleteContactRequest request);
    }
}