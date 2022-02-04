using System;
using IdentityServer.Domain.Generic.Enums;
using MediatR;
using XFramework.Domain.Generic.BusinessObjects;
using XFramework.Domain.Generic.Contracts.Requests.Create;
using XFramework.Domain.Generic.Enums;

namespace XFramework.Core.DataAccess.Commands.Entity.Identity
{
    public class CreateIdentityCmd : CreateUserRequest, IRequest<CmdResponseBO>
    {
    }
}