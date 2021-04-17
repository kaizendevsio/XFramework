using System;
using System.Net;
using System.Threading.Tasks;
using IdentityServer.Domain.Contracts;
using XFramework.Domain.Generic.BusinessObjects;
using XFramework.Integration.Interfaces;
using XFramework.Integration.Interfaces.Wrappers;

namespace XFramework.Integration.Drivers
{
    public class IdentityServerDriver : IIdentityServiceWrapper
    {
        public IMessageBusWrapper StreamFlowDriverSignalR { get; }

        public IdentityServerDriver(IMessageBusWrapper streamFlowDriverSignalR)
        {
            StreamFlowDriverSignalR = streamFlowDriverSignalR;
            StreamFlowDriverSignalR.TargetClient = new Guid("3902761a-822d-4c6b-8e2d-323fd501bcd6");
        }
        
        public async Task<QueryResponseBO<AuthorizeIdentityContract>> Authenticate()
        {
            await StreamFlowDriverSignalR.Push(new()
            {
                MethodName = ""
            });

            return new()
            {
                
            };
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