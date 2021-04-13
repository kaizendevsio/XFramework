using MediatR;
using System;
using System.Threading;
using System.Threading.Tasks;
using XFramework.Core.DataAccess.Commands.Entity.Application;
using XFramework.Core.Interfaces;

namespace XFramework.Core.DataAccess.Commands.Handlers.Application
{
    public class GetAppInfoHandler : CommandBaseHandler, IRequestHandler<GetAppInfoCmd, bool>
    {
        public GetAppInfoHandler(IDataLayer dataLayer)
        {
            DataLayer = dataLayer;
        }
        public Task<bool> Handle(GetAppInfoCmd request, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}
