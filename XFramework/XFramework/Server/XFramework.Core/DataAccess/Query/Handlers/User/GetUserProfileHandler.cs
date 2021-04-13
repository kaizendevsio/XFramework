using MediatR;
using System;
using System.Threading;
using System.Threading.Tasks;
using XFramework.Core.DataAccess.Query.Entity.User;

namespace XFramework.Core.DataAccess.Query.Handlers.User
{
    public class GetUserProfileHandler : QueryBaseHandler, IRequestHandler<GetUserProfileQuery, UserBO>
    {
        public Task<UserBO> Handle(GetUserProfileQuery request, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}
