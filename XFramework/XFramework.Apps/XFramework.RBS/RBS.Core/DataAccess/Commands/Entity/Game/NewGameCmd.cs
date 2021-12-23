using MediatR;
using RBS.Domain.BusinessObjects;

namespace RBS.Core.DataAccess.Commands.Entity.Game
{
    public class NewGameCmd : CommandBaseEntity, IRequest<CmdResponseBO<NewGameCmd>>
    {
        
    }
}