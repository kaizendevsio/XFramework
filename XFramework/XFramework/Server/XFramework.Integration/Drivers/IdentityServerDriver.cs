using System;
using System.Net;
using System.Threading.Tasks;
using IdentityServer.Domain.Generic.Contracts.Responses;
using Microsoft.Extensions.Configuration;
using XFramework.Domain.Generic.BusinessObjects;
using XFramework.Integration.Interfaces.Wrappers;

namespace XFramework.Integration.Drivers
{
    public class IdentityServerDriver : DriverBase, IIdentityServiceWrapper
    {
        public IMessageBusWrapper StreamFlowDriverSignalR { get; }

        public IdentityServerDriver(IMessageBusWrapper streamFlowDriverSignalR, IConfiguration configuration)
        {
            StreamFlowDriverSignalR = streamFlowDriverSignalR;
            Configuration = configuration;
            StreamFlowDriverSignalR.TargetClient = Configuration.GetValue<Guid>("StreamFlowConfiguration:Targets:IdentityServerService");
        }
        
        public async Task<QueryResponseBO<AuthorizeIdentityContract>> Authenticate()
        {
            /*await StreamFlowDriverSignalR.Push(new()
            {
                MethodName = ""
            });*/

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