using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using RBS.Core.DataAccess.Commands.Entity.Game;
using RBS.Core.Interfaces;
using RBS.Domain.BusinessObjects;

namespace RBS.Core.DataAccess.Commands.Handlers.Game
{
    public class NewGameHandler : CommandBaseHandler, IRequestHandler<NewGameCmd, CmdResponseBO<NewGameCmd>>
    {
        public NewGameHandler(IMediator mediator, IDataLayer dataLayer)
        {
            _mediator = mediator;
            _dataLayer = dataLayer;
        }
        
        public async Task<CmdResponseBO<NewGameCmd>> Handle(NewGameCmd request, CancellationToken cancellationToken)
        {
            _dataLayer.FightHistories.Add(new()
            {
                CategoryId = 1,
                BetWala = 0,
                BetMeron = 0,
                BetDraw = 0,
                Uuid = Guid.NewGuid().ToString()
            });
             await _dataLayer.SaveChangesAsync(cancellationToken);

             return new()
             {
                 HttpStatusCode = HttpStatusCode.Accepted
             };
        }
    }
}