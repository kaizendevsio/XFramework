using Microsoft.Extensions.Logging;
using XFramework.Core.DataAccess.Commands;
using XFramework.Domain.Contexts;
using XFramework.Domain.Generic.Contracts;

namespace IdentityServer.Core.DataAccess.Commands.Credential;

public class CreateCredential(
        AppDbContext appDbContext,
        ILogger<CreateCredential> logger,
        IEnumerable<IRequestHandler<Create<IdentityCredential>, CmdResponse<IdentityCredential>>> requestHandlers
    ) 
    : ICreateHandler<IdentityCredential>
{
    public async Task<CmdResponse<IdentityCredential>> Handle(Create<IdentityCredential> request, CancellationToken cancellationToken)
    {
        var hashPasswordByte = Encoding.ASCII.GetBytes(BCrypt.Net.BCrypt.HashPassword(inputKey: request.Model.Password, workFactor:11));
        request.Model.PasswordByte = hashPasswordByte;

        await requestHandlers.First().Handle(request, cancellationToken);
        
        return new ()
        {
            Response = request.Model,
            HttpStatusCode = HttpStatusCode.OK
        };
    }
}