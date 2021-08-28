using System;
using MediatR;
using XFramework.Domain.Generic.BusinessObjects;
using XFramework.Domain.Generic.Contracts.Responses;

namespace IdentityServer.Core.DataAccess.Query.Entity.Identity.Credential
{
    public class CheckCredentialExistenceQuery : QueryBaseEntity, IRequest<QueryResponseBO<ExistenceContract>>
    {
        public string UserName { get; set; }
        public Guid Cuid { get; set; }
    }
}