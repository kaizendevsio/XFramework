using System;
using IdentityServer.Domain.Generic.Contracts.Responses;
using MediatR;
using XFramework.Domain.Generic.BusinessObjects;

namespace IdentityServer.Core.DataAccess.Query.Entity.Identity
{
    public class GetIdentityQuery : QueryBaseEntity, IRequest<QueryResponseBO<IdentityInfoContract>>
    {
        public Guid Uid { get; set; }   
    }
}