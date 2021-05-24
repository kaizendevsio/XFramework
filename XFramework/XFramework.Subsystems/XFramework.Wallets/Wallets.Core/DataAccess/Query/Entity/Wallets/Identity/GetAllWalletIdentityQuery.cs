using System.Collections.Generic;
using MediatR;
using Wallets.Core.DataAccess.Commands.Entity;
using Wallets.Domain.Generic.Contracts.Responses;
using XFramework.Domain.Generic.BusinessObjects;

namespace Wallets.Core.DataAccess.Query.Entity.Wallets.Identity
{
    public class GetAllWalletIdentityQuery : CommandBaseEntity, IRequest<QueryResponseBO<List<GetWalletIdentityQuery>>>, IRequest<QueryResponseBO<List<GetWalletIdentityContract>>>
    {
        
    }
}