using XFramework.Core.DataAccess.Query;
using XFramework.Core.Services;
using XFramework.Domain.Generic.Contracts.Responses;

namespace IdentityServer.Core.DataAccess.Commands.Credential;

public class CreateCredential(
    AppDbContext appDbContext,
    ILogger<CreateCredential> logger,
    ITenantService tenantService,
    IRequestHandler<Create<IdentityCredential>, CmdResponse<IdentityCredential>> baseHandler
)
    : ICreateHandler<IdentityCredential>, IDecorator
{
    public async Task<CmdResponse<IdentityCredential>> Handle(Create<IdentityCredential> request,
        CancellationToken cancellationToken)
    {
        var hashPasswordByte = Encoding.ASCII.GetBytes(BCrypt.Net.BCrypt.HashPassword(inputKey: request.Model.Password, workFactor: 11));
        request.Model.PasswordByte = hashPasswordByte;

        await baseHandler.Handle(request, cancellationToken);

        return new()
        {
            Response = request.Model,
            HttpStatusCode = HttpStatusCode.OK
        };
    }
}