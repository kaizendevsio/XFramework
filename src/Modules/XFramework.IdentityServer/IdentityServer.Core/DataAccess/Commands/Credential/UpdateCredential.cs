using XFramework.Core.Services;

namespace IdentityServer.Core.DataAccess.Commands.Credential;

public class UpdateCredential(
        DbContext appDbContext,
        ILogger<UpdateCredential> logger,
        ITenantService tenantService,
        IRequestHandler<Patch<IdentityCredential>, CmdResponse<IdentityCredential>> baseHandler
    )
    : IPatchHandler<IdentityCredential>, IDecorator
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

        return await baseHandler.Handle(request, cancellationToken);
    }
}