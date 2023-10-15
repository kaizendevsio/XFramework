using Microsoft.Extensions.Logging;
using XFramework.Core.DataAccess.Commands;
using XFramework.Domain.Contexts;
using XFramework.Domain.Generic.Contracts;

namespace IdentityServer.Core.DataAccess.Commands.Credential;

public class UpdateCredential(
        AppDbContext appDbContext,
        ILogger<UpdateCredential> logger,
        IEnumerable<IRequestHandler<Patch<IdentityCredential>, CmdResponse<IdentityCredential>>> requestHandlers
    )
    : IRequestHandler<Patch<IdentityCredential>, CmdResponse<IdentityCredential>>
{
    public async Task<CmdResponse<IdentityCredential>> Handle(Patch<IdentityCredential> request, CancellationToken cancellationToken)
    {
        /*
       var response = await _mediator.Send(new CheckVerificationQuery
       {
           RequestServer = request.RequestServer,
           CredentialGuid = Guid.Parse(entity.Id),
           VerificationTypeGuid = Guid.Parse("45a7a8a7-3735-4a58-b93f-aa9e7b24a7c4")

       });

       if (response.HttpStatusCode is not HttpStatusCode.Accepted)
       {
           return new()
           {
               HttpStatusCode = HttpStatusCode.BadRequest,
               Message = "Invalid verification code",
               Request = request
           };
       }*/
        
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