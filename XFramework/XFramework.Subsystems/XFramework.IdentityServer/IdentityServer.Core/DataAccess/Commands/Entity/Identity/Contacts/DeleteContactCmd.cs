using IdentityServer.Domain.Generic.Contracts.Requests.Delete;

namespace IdentityServer.Core.DataAccess.Commands.Entity.Identity.Contacts;

public class DeleteContactCmd : DeleteContactRequest, IRequest<CmdResponseBO<DeleteContactCmd>>
{

}