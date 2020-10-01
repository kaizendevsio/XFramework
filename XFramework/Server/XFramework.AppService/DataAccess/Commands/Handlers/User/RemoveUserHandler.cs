using MediatR;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using XFramework.Core.DataAccess.Commands.Entity.User;

namespace XFramework.Core.DataAccess.Commands.Handlers.User
{
    public class RemoveUserHandler : CommandBaseHandler, IRequestHandler<RemoveUserCmd, bool>
    {
        public async Task<bool> Handle(RemoveUserCmd request, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}
