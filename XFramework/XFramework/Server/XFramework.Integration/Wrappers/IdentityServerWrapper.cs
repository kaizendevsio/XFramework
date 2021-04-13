using System.Net;
using System.Threading.Tasks;
using IdentityServer.Domain.Contracts;
using XFramework.Domain.Generic.BusinessObjects;
using XFramework.Integration.Interfaces;

namespace XFramework.Integration.Wrappers
{
    public class IdentityServerWrapper : IIdentityServerWrapper
    {
        public Task<QueryResponseBO<AuthorizeIdentityContract>> Authenticate()
        {
            throw new System.NotImplementedException();
        }

        public Task<CmdResponseBO<HttpStatusCode>> CreateCredential()
        {
            throw new System.NotImplementedException();
        }

        public Task<CmdResponseBO<HttpStatusCode>> UpdateCredential()
        {
            throw new System.NotImplementedException();
        }

        public Task<CmdResponseBO<HttpStatusCode>> DeleteCredential()
        {
            throw new System.NotImplementedException();
        }
        

        public Task<CmdResponseBO<HttpStatusCode>> GetIdentity()
        {
            throw new System.NotImplementedException();
        }

        public Task<CmdResponseBO<HttpStatusCode>> CreateIdentity()
        {
            throw new System.NotImplementedException();
        }

        public Task<CmdResponseBO<HttpStatusCode>> UpdateIdentity()
        {
            throw new System.NotImplementedException();
        }

        public Task<CmdResponseBO<HttpStatusCode>> DeleteIdentity()
        {
            throw new System.NotImplementedException();
        }
    }
}