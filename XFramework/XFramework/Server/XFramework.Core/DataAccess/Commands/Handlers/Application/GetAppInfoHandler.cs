using MediatR;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using XFramework.Core.DataAccess.Commands.Entity.Application;
using XFramework.Core.Interfaces;

namespace XFramework.Core.DataAccess.Commands.Handlers.Application
{
    public class GetAppInfoHandler : CommandBaseHandler, IRequestHandler<GetAppInfoCmd, bool>
    {
        public GetAppInfoHandler()
        {
        }
        public async Task<bool> Handle(GetAppInfoCmd request, CancellationToken cancellationToken)
        {
            return true;
        }
    }
}
