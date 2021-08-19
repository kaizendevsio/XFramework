using System.Collections.Generic;
using IdentityServer.Domain.Generic.Contracts.Responses;
using MediatR;
using XFramework.Domain.Generic.BusinessObjects;
using XFramework.Domain.Generic.Enums;

namespace IdentityServer.Core.DataAccess.Query.Entity.Identity.Credential
{
    public class GetIdentityCredentialListQuery : QueryBaseEntity, IRequest<QueryResponseBO<List<IdentityCredentialContract>>>
    {
        public IdentityRole IdentityRole { get; set; }
    }
}   