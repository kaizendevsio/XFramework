using MediatR;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using XFramework.Core.DataAccess.Commands.Entity.User;

namespace XFramework.Core.DataAccess.Commands.Handlers.User
{
    public class UpdateUserHandler : CommandBaseHandler, IRequestHandler<ValidateUserEmailCmd, bool>
    {
        public Task<bool> Handle(ValidateUserEmailCmd request, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}
