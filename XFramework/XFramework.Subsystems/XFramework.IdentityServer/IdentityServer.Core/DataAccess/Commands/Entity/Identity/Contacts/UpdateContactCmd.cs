using IdentityServer.Domain.Generic.Contracts.Requests.Update;

namespace IdentityServer.Core.DataAccess.Commands.Entity.Identity.Contacts;

public class UpdateContactCmd : UpdateContactRequest, IRequest<CmdResponseBO<UpdateContactCmd>>
{

}