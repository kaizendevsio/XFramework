using IdentityServer.Domain.Generic.Contracts.Requests.Create.Storage;

namespace IdentityServer.Core.DataAccess.Commands.Entity.Storage;

public class CreateFileCmd : CreateFileRequest, IRequest<CmdResponse<CreateFileCmd>>
{
    
}