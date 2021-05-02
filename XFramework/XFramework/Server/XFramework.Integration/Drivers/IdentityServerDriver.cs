using System;
using System.Net;
using System.Threading.Tasks;
using IdentityServer.Domain.Generic.Contracts.Requests;
using IdentityServer.Domain.Generic.Contracts.Responses;
using Mapster;
using Microsoft.Extensions.Configuration;
using StreamFlow.Domain.Generic.Enums;
using XFramework.Domain.Generic.BusinessObjects;
using XFramework.Integration.Interfaces.Wrappers;

namespace XFramework.Integration.Drivers
{
    public class IdentityServerDriver : DriverBase, IIdentityServiceWrapper
    {
        public IMessageBusWrapper StreamFlowDriver { get; }

        public IdentityServerDriver(IMessageBusWrapper streamFlowDriver, IConfiguration configuration)
        {
            StreamFlowDriver = streamFlowDriver;
            Configuration = configuration;
            StreamFlowDriver.TargetClient = Configuration.GetValue<Guid>("StreamFlowConfiguration:Targets:IdentityServerService");
        }
        
        public async Task<QueryResponseBO<AuthorizeIdentityContract>> Authenticate(AuthenticateCredentialRequest request)
        {
            var result = await StreamFlowDriver.InvokeAsync<ApiStatusBO>(new(request)
            {
                CommandName = "CreateIdentity",
                ExchangeType = MessageExchangeType.Direct,
            });
            return result.Adapt<QueryResponseBO<AuthorizeIdentityContract>>();
        }

        public async Task<CmdResponseBO> CreateCredential(CreateCredentialRequest request)
        {
            var result = await StreamFlowDriver.InvokeAsync<ApiStatusBO>(new(request)
            {
                CommandName = "CreateIdentity",
                ExchangeType = MessageExchangeType.Direct,
            });
            return result.Adapt<CmdResponseBO>();
        }

        public async Task<CmdResponseBO> UpdateCredential(UpdateCredentialRequest request)
        {
            var result = await StreamFlowDriver.InvokeAsync<ApiStatusBO>(new(request)
            {
                CommandName = "CreateIdentity",
                ExchangeType = MessageExchangeType.Direct,
            });
            return result.Adapt<CmdResponseBO>();
        }

        public async Task<CmdResponseBO> DeleteCredential(UpdateCredentialRequest request)
        {
            var result = await StreamFlowDriver.InvokeAsync<ApiStatusBO>(new(request)
            {
                CommandName = "CreateIdentity",
                ExchangeType = MessageExchangeType.Direct,
            });
            return result.Adapt<CmdResponseBO>();
        }
        

        public async Task<CmdResponseBO> GetIdentity()
        {
            throw new System.NotImplementedException();
        }

        public async Task<CmdResponseBO> CreateIdentity(CreateIdentityRequest request)
        {
            var result = await StreamFlowDriver.InvokeAsync<ApiStatusBO>(new(request)
            {
                CommandName = "CreateIdentity",
                ExchangeType = MessageExchangeType.Direct,
            });
            return result.Adapt<CmdResponseBO>();
        }

        public async Task<CmdResponseBO> UpdateIdentity(UpdateIdentityRequest request)
        {
            var result = await StreamFlowDriver.InvokeAsync<ApiStatusBO>(new(request)
            {
                CommandName = "CreateIdentity",
                ExchangeType = MessageExchangeType.Direct,
            });
            return result.Adapt<CmdResponseBO>();
        }

        public async Task<CmdResponseBO> DeleteIdentity(DeleteIdentityRequest request)
        {
            var result = await StreamFlowDriver.InvokeAsync<ApiStatusBO>(new(request)
            {
                CommandName = "CreateIdentity",
                ExchangeType = MessageExchangeType.Direct,
            });
            return result.Adapt<CmdResponseBO>();
        }
    }
}