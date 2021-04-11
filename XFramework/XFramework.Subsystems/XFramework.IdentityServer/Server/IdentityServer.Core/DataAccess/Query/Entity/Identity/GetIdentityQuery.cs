using System;
using IdentityServer.Domain.Contracts;
using MediatR;
using XFramework.Domain.Generic.BusinessObjects;

namespace IdentityServer.Core.DataAccess.Query.Entity.Identity
{
    public class GetIdentityQuery : QueryBaseEntity, IRequest<QueryResponseBO<GetIdentityContract>>
    {
        public Guid Uid { get; set; }   
    }
}