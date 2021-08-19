using MediatR;
using RBS.Core.Interfaces;

namespace RBS.Core.DataAccess.Commands.Handlers
{
    public class CommandBaseHandler
    {
        public IDataLayer _dataLayer;
        public IMediator _mediator;

    }
}
