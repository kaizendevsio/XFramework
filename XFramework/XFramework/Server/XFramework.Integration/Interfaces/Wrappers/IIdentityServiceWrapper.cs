using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using IdentityServer.Domain.Generic.Contracts.Requests;
using IdentityServer.Domain.Generic.Contracts.Responses;
using XFramework.Domain.Generic.BusinessObjects;
using XFramework.Domain.Generic.Contracts.Responses;
using XFramework.Domain.Generic.Interfaces;

namespace XFramework.Integration.Interfaces.Wrappers
{
    public interface IIdentityServiceWrapper : IXFrameworkService
    {
        public Task<QueryResponseBO<AuthorizeIdentityContract>> AuthenticateCredential(AuthenticateCredentialRequest request);
        public Task<CmdResponseBO> CreateCredential(CreateCredentialRequest request);
        public Task<CmdResponseBO> UpdateCredential(UpdateCredentialRequest request);
        public Task<CmdResponseBO> DeleteCredential(DeleteCredentialRequest request);
        public Task<CmdResponseBO> ChangePassword(ChangePasswordRequest request);
        public Task<QueryResponseBO<ExistenceContract>> CheckCredentialExistence(CheckCredentialExistenceRequest request);
        public Task<QueryResponseBO<List<IdentityCredentialContract>>> GetIdentityCredentialList(GetIdentityCredentialListRequest request);
        
        public Task<QueryResponseBO<List<IdentityCredentialContract>>> GetIdentityRoleList(GetIdentityRoleListRequest request);
        public Task<QueryResponseBO<List<IdentityRoleEntityContract>>> GetRoleEntityList(GetRoleEntityListRequest request);
        
        public Task<CmdResponseBO> CreateIdentity(CreateIdentityRequest request);
        public Task<CmdResponseBO> UpdateIdentity(UpdateIdentityRequest request);
        public Task<CmdResponseBO> DeleteIdentity(DeleteIdentityRequest request);
        public Task<QueryResponseBO<ExistenceContract>> CheckIdentityExistence(CheckIdentityExistenceRequest request);
        public Task<QueryResponseBO<IdentityInfoContract>> GetIdentity(GetIdentityRequest request);
        
        public Task<CmdResponseBO> CreateContact(CreateContactRequest request);
        public Task<CmdResponseBO> UpdateContact(UpdateContactRequest request);
        public Task<CmdResponseBO> DeleteContact(DeleteContactRequest request);
        public Task<QueryResponseBO<ExistenceContract>> CheckContactExistence(CheckContactExistenceRequest request);
    }
}