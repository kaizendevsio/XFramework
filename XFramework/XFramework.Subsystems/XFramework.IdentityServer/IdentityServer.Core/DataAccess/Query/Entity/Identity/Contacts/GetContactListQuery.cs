﻿using IdentityServer.Domain.Generic.Contracts.Requests.Get;
using IdentityServer.Domain.Generic.Contracts.Responses;
using XFramework.Domain.Generic.Contracts.Responses;

namespace IdentityServer.Core.DataAccess.Query.Entity.Identity.Contacts;

public class GetContactListQuery : GetContactListRequest, IRequest<QueryResponseBO<List<ContactResponse>>>
{
    
}