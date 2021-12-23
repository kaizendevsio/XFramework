using MediatR;
using RBS.Domain.BusinessObjects;
using RBS.Domain.Enums;

namespace RBS.Core.DataAccess.Commands.Entity.Game
{
    public class CheckWinnerCmd : CommandBaseEntity, IRequest<CmdResponseBO<CheckWinnerCmd>>
    {
        public BetOption BetOption { get; set; }   
    }
}