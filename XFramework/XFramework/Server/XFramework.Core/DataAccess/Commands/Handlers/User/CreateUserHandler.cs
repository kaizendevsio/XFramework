using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using IdentityServer.Domain.Generic.Contracts.Requests;
using Mapster;
using MediatR;
using XFramework.Core.DataAccess.Commands.Entity.User;
using XFramework.Core.Interfaces;
using XFramework.Domain.DataTransferObjects;
using XFramework.Domain.Generic.BusinessObjects;
using XFramework.Integration.Interfaces.Wrappers;

namespace XFramework.Core.DataAccess.Commands.Handlers.User
{
    public class CreateUserHandler : CommandBaseHandler, IRequestHandler<CreateUserCmd, CmdResponseBO<CreateUserCmd>>
    {
        public CreateUserHandler(IIdentityServiceWrapper identityServiceWrapper)
        {
            IdentityServiceWrapper = identityServiceWrapper;
        }

        public async Task<CmdResponseBO<CreateUserCmd>> Handle(CreateUserCmd request, CancellationToken cancellationToken)
        {
            var response = await IdentityServiceWrapper.CreateIdentity(request.Adapt<CreateIdentityRequest>());
            if (response.HttpStatusCode != HttpStatusCode.Accepted)
            {
                return new()
                {
                    HttpStatusCode = response.HttpStatusCode,
                    Message = response.Message
                };
            }

            response = await IdentityServiceWrapper.CreateCredential(request.Adapt<CreateCredentialRequest>());
            if (response.HttpStatusCode != HttpStatusCode.Accepted)
            {
                return new()
                {
                    HttpStatusCode = response.HttpStatusCode,
                    Message = response.Message
                };
            }

            return new()
            {
                HttpStatusCode = HttpStatusCode.Accepted
            };
        }
    }
}