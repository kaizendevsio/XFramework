using MediatR;

namespace XFramework.Core.DataAccess.Query.Entity.User
{
   public class GetUserProfileQuery : UserAuthBO, IRequest<UserBO>
    {
    }
}
