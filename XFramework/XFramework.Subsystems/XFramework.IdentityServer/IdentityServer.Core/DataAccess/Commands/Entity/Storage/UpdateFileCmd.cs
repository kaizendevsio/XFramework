using IdentityServer.Domain.Generic.Contracts.Requests.Create.Storage;

namespace IdentityServer.Core.DataAccess.Commands.Entity.Storage;

public class UpdateFileCmd : UpdateFileRequest, IRequest<CmdResponse<UpdateFileCmd>>
{
    
}