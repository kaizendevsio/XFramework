using System;
using MediatR;
using XFramework.Domain.Generic.BusinessObjects;
using XFramework.Domain.Generic.Contracts.Responses;

namespace IdentityServer.Core.DataAccess.Query.Entity.Identity
{
    public class CheckIdentityExistenceQuery : QueryBaseEntity, IRequest<QueryResponseBO<ExistenceContract>>
    {
        public string FirstName { get; set; }
        public string MiddleName { get; set; }
        public string LastName { get; set; }
        
        public string Uid { get; set; }
    }
}