using System.Net;
using System.Threading.Tasks;
using IdentityServer.Domain.Contracts;
using XFramework.Domain.Generic.BusinessObjects;
using XFramework.Domain.Generic.Interfaces;

namespace XFramework.Integration.Interfaces.Wrappers
{
    public interface IIdentityServiceWrapper : IXFrameworkService
    {
        public Task<QueryResponseBO<AuthorizeIdentityContract>> Authenticate();
        public Task<CmdResponseBO<HttpStatusCode>> CreateCredential();
        public Task<CmdResponseBO<HttpStatusCode>> UpdateCredential();
        public Task<CmdResponseBO<HttpStatusCode>> DeleteCredential();
        
        public Task<CmdResponseBO<HttpStatusCode>> GetIdentity();
        public Task<CmdResponseBO<HttpStatusCode>> CreateIdentity();
        public Task<CmdResponseBO<HttpStatusCode>> UpdateIdentity();
        public Task<CmdResponseBO<HttpStatusCode>> DeleteIdentity();
    }
}