using IdentityServer.Domain.Generic.Contracts.Requests.Create;

namespace IdentityServer.Core.DataAccess.Commands.Entity.Identity.Contacts;

public class CreateContactCmd : CreateContactRequest, IRequest<CmdResponseBO<CreateContactCmd>>
{
    
}