using XFramework.Core.DataAccess.Query;
using XFramework.Domain.Generic.Contracts.Responses;

namespace IdentityServer.Core.DataAccess.Commands.Credential;

public class CreateCredential(
        AppDbContext appDbContext,
        ILogger<CreateCredential> logger,
        IRequestHandler<Create<IdentityCredential>, CmdResponse<IdentityCredential>> baseHandler
    ) 
    :  ICreateHandler<IdentityCredential>, IDecorator
{
    public async Task<CmdResponse<IdentityCredential>> Handle(Create<IdentityCredential> request, CancellationToken cancellationToken)
    {
        var hashPasswordByte = Encoding.ASCII.GetBytes(BCrypt.Net.BCrypt.HashPassword(inputKey: request.Model.Password, workFactor:11));
        request.Model.PasswordByte = hashPasswordByte;

        await baseHandler.Handle(request, cancellationToken);
        
        return new ()
        {
            Response = request.Model,
            HttpStatusCode = HttpStatusCode.OK
        };
    }
} 


public class GetCredentialList(
        AppDbContext appDbContext,
        ILogger<GetCredentialList> logger,
        IRequestHandler<GetList<IdentityCredential>, QueryResponse<PaginatedResult<IdentityCredential>>> baseHandler
    ) 
    :  IGetListHandler<IdentityCredential>, IDecorator
{
    public async Task<QueryResponse<PaginatedResult<IdentityCredential>>> Handle(GetList<IdentityCredential> request, CancellationToken cancellationToken)
    {
        return new()
        {
            Response = null,
            HttpStatusCode = HttpStatusCode.OK
        };
    }
} 