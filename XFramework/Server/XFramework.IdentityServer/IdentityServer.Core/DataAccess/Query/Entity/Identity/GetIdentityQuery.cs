using System;
using IdentityServer.Domain.BO;
using IdentityServer.Domain.Contracts;
using MediatR;

namespace IdentityServer.Core.DataAccess.Query.Entity.Identity
{
    public class GetIdentityQuery : IRequest<QueryResponseBO<GetIdentityContract>>
    {
        public Guid Uid { get; set; }   
    }
}