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
        public IdentityServerDriver(IMessageBusWrapper streamFlowDriver, IConfiguration configuration)
        {
            StreamFlowDriver = streamFlowDriver;
            Configuration = configuration;
            TargetClient = Guid.Parse(Configuration.GetValue<string>("StreamFlowConfiguration:Targets:IdentityServerService"));
        }
        
        public async Task<QueryResponseBO<AuthorizeIdentityContract>> Authenticate(AuthenticateCredentialRequest request)
        {
            var result = await StreamFlowDriver.InvokeAsync<CmdResponseBO>(new(request)
            {
                CommandName = "Authenticate",
                ExchangeType = MessageExchangeType.Direct,
                Recipient = TargetClient
            });
            return result.Response.Adapt<QueryResponseBO<AuthorizeIdentityContract>>();
        }

        public async Task<CmdResponseBO> CreateCredential(CreateCredentialRequest request)
        {
            var result = await StreamFlowDriver.InvokeAsync<CmdResponseBO>(new(request)
            {
                CommandName = "CreateCredential",
                ExchangeType = MessageExchangeType.Direct,
                Recipient = TargetClient
            });
            return result.Response.Adapt<CmdResponseBO>();
        }

        public async Task<CmdResponseBO> UpdateCredential(UpdateCredentialRequest request)
        {
            var result = await StreamFlowDriver.InvokeAsync<CmdResponseBO>(new(request)
            {
                CommandName = "UpdateCredential",
                ExchangeType = MessageExchangeType.Direct,
                Recipient = TargetClient
            });
            return result.Response.Adapt<CmdResponseBO>();
        }

        public async Task<CmdResponseBO> DeleteCredential(DeleteCredentialRequest request)
        {
            var result = await StreamFlowDriver.InvokeAsync<CmdResponseBO>(new(request)
            {
                CommandName = "DeleteCredential",
                ExchangeType = MessageExchangeType.Direct,
                Recipient = TargetClient
            });
            return result.Response.Adapt<CmdResponseBO>();
        }
        

        public async Task<QueryResponseBO<GetIdentityContract>> GetIdentity(GetIdentityRequest request)
        {
            var result = await StreamFlowDriver.InvokeAsync<CmdResponseBO>(new(request)
            {
                CommandName = "GetIdentity",
                ExchangeType = MessageExchangeType.Direct,
                Recipient = TargetClient
            });
            return result.Response.Adapt<QueryResponseBO<GetIdentityContract>>();
        }

        public async Task<CmdResponseBO> CreateIdentity(CreateIdentityRequest request)
        {
            var result = await StreamFlowDriver.InvokeAsync<CmdResponseBO>(new(request)
            {
                CommandName = "CreateIdentity",
                ExchangeType = MessageExchangeType.Direct,
                Recipient = TargetClient
            });
            return result.Response.Adapt<CmdResponseBO>();
        }

        public async Task<CmdResponseBO> UpdateIdentity(UpdateIdentityRequest request)
        {
            var result = await StreamFlowDriver.InvokeAsync<CmdResponseBO>(new(request)
            {
                CommandName = "UpdateIdentity",
                ExchangeType = MessageExchangeType.Direct,
                Recipient = TargetClient
            });
            return result.Response.Adapt<CmdResponseBO>();
        }

        public async Task<CmdResponseBO> DeleteIdentity(DeleteIdentityRequest request)
        {
            var result = await StreamFlowDriver.InvokeAsync<CmdResponseBO>(new(request)
            {
                CommandName = "DeleteIdentity",
                ExchangeType = MessageExchangeType.Direct,
                Recipient = TargetClient
            });
            return result.Response.Adapt<CmdResponseBO>();
        }
    }
}