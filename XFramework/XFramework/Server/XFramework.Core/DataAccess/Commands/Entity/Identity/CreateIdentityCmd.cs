using XFramework.Domain.Generic.Contracts.Requests.Create;

namespace XFramework.Core.DataAccess.Commands.Entity.Identity
{
    public class CreateIdentityCmd : CreateUserRequest, IRequest<CmdResponse>
    {
    }
}