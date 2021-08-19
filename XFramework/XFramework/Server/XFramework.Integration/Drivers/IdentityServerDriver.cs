using System;
using System.Collections.Generic;
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
        public IdentityServerDriver(IMessageBusWrapper messageBusDriver, IConfiguration configuration)
        {
            MessageBusDriver = messageBusDriver;
            Configuration = configuration;
            TargetClient =
                Guid.Parse(Configuration.GetValue<string>("StreamFlowConfiguration:Targets:IdentityServerService"));
        }

        public async Task<QueryResponseBO<AuthorizeIdentityContract>> AuthenticateCredential(AuthenticateCredentialRequest request)
        {
            return await SendAsync<AuthenticateCredentialRequest, AuthorizeIdentityContract>("Authenticate", request);
        }

        public async Task<CmdResponseBO> CreateCredential(CreateCredentialRequest request)
        {
            var result = await SendAsync<CreateCredentialRequest, CmdResponseBO>("CreateCredential", request);
            return result.Adapt<CmdResponseBO>();
        }

        public async Task<CmdResponseBO> UpdateCredential(UpdateCredentialRequest request)
        {
            var result = await SendAsync<UpdateCredentialRequest, CmdResponseBO>("UpdateCredential", request);
            return result.Adapt<CmdResponseBO>();
        }

        public async Task<CmdResponseBO> DeleteCredential(DeleteCredentialRequest request)
        {
            var result = await SendAsync<DeleteCredentialRequest, CmdResponseBO>("DeleteCredential", request);
            return result.Adapt<CmdResponseBO>();
        }


        public async Task<QueryResponseBO<IdentityInfoContract>> GetIdentity(GetIdentityRequest request)
        {
            return await SendAsync<GetIdentityRequest, IdentityInfoContract>("GetIdentity", request);
        }

        public async Task<QueryResponseBO<List<IdentityCredentialContract>>> GetIdentityCredentialList(GetIdentityCredentialListRequest request)
        {
            return await SendAsync<GetIdentityCredentialListRequest, List<IdentityCredentialContract>>("GetIdentityCredentialList", request);
        }

        public async Task<CmdResponseBO> CreateIdentity(CreateIdentityRequest request)
        {
            var result = await SendAsync<CreateIdentityRequest, CmdResponseBO>("CreateIdentity", request);
            return result.Adapt<CmdResponseBO>();
        }

        public async Task<CmdResponseBO> UpdateIdentity(UpdateIdentityRequest request)
        {
            var result = await SendAsync<UpdateIdentityRequest, CmdResponseBO>("UpdateIdentity", request);
            return result.Adapt<CmdResponseBO>();
        }

        public async Task<CmdResponseBO> DeleteIdentity(DeleteIdentityRequest request)
        {
            var result = await SendAsync<DeleteIdentityRequest, CmdResponseBO>("DeleteIdentity", request);
            return result.Adapt<CmdResponseBO>();
        }
    }
}