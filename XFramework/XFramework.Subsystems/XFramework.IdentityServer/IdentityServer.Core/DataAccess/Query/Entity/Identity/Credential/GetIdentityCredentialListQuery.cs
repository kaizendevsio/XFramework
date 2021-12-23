using System.Collections.Generic;
using IdentityServer.Domain.Generic.Contracts.Responses;
using IdentityServer.Domain.Generic.Enums;
using MediatR;
using XFramework.Domain.Generic.BusinessObjects;
using XFramework.Domain.Generic.Enums;

namespace IdentityServer.Core.DataAccess.Query.Entity.Identity.Credential
{
    public class GetIdentityCredentialListQuery : QueryBaseEntity, IRequest<QueryResponseBO<List<IdentityCredentialContract>>>
    {
        public RoleEntity IdentityRole { get; set; }
        public string SearchString { get; set; }
        public int ListCount { get; set; } = 50;
        public bool Filter { get; set; }
        public int Skip { get; set; }
    }
}   