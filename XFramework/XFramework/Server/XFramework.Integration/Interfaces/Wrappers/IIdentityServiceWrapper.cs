using System.Net;
using System.Threading.Tasks;
using IdentityServer.Domain.Generic.Contracts.Requests;
using IdentityServer.Domain.Generic.Contracts.Responses;
using XFramework.Domain.Generic.BusinessObjects;
using XFramework.Domain.Generic.Interfaces;

namespace XFramework.Integration.Interfaces.Wrappers
{
    public interface IIdentityServiceWrapper : IXFrameworkService
    {
        public Task<QueryResponseBO<AuthorizeIdentityContract>> Authenticate(AuthenticateCredentialRequest request);
        public Task<CmdResponseBO> CreateCredential(CreateCredentialRequest request);
        public Task<CmdResponseBO> UpdateCredential(UpdateCredentialRequest request);
        public Task<CmdResponseBO> DeleteCredential(UpdateCredentialRequest request);
        
        public Task<CmdResponseBO> GetIdentity();
        public Task<CmdResponseBO> CreateIdentity(CreateIdentityRequest request);
        public Task<CmdResponseBO> UpdateIdentity(UpdateIdentityRequest request);
        public Task<CmdResponseBO> DeleteIdentity(DeleteIdentityRequest request);
    }
}