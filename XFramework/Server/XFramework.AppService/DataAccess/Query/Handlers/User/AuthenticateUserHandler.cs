using MediatR;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using XFramework.Core.DataAccess.Commands.Handlers;
using XFramework.Core.DataAccess.Query.Entity.User;

namespace XFramework.Core.DataAccess.Query.Handlers.User
{
    public class AuthenticateUserHandler : QueryBaseHandler, IRequestHandler<AuthenticateUserQuery, bool>
    {
        public async Task<bool> Handle(AuthenticateUserQuery request, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}
