using System.Collections.Generic;
using MediatR;
using Wallets.Core.DataAccess.Commands.Entity;
using Wallets.Domain.Generic.Contracts.Responses;
using XFramework.Domain.Generic.BusinessObjects;

namespace Wallets.Core.DataAccess.Query.Entity.Wallets
{
    public class GetAllWalletQuery : CommandBaseEntity, IRequest<QueryResponseBO<List<GetWalletContract>>>
    {
    }
}