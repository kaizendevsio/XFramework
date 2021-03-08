﻿using System;
using IdentityServer.Domain.BusinessObjects;
using MediatR;

namespace IdentityServer.Core.DataAccess.Commands.Entity.Identity.Authorization
{
    public class UpdateAuthorizeIdentityCmd : CommandBaseEntity, IRequest<CmdResponseBO<UpdateAuthorizeIdentityCmd>>
    {
        public Guid Uid { get; set; }
        public string Username { get; set; }
    }
}