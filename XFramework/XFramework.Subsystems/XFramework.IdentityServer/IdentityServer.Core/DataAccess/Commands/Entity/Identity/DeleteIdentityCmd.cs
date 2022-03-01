using IdentityServer.Domain.Generic.Contracts.Requests.Delete;

namespace IdentityServer.Core.DataAccess.Commands.Entity.Identity;

public class DeleteIdentityCmd : DeleteIdentityRequest, IRequest<CmdResponse<DeleteIdentityCmd>>
{
}