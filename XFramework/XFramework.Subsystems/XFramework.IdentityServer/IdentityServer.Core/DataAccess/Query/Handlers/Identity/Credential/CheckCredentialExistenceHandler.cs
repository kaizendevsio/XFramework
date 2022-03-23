using System.Text.RegularExpressions;
using IdentityServer.Core.DataAccess.Query.Entity.Identity.Credentials;
using IdentityServer.Domain.DataTransferObjects.Legacy;
using XFramework.Domain.Generic.Contracts.Responses;

namespace IdentityServer.Core.DataAccess.Query.Handlers.Identity.Credential;

public class CheckCredentialExistenceHandler : QueryBaseHandler ,IRequestHandler<CheckCredentialExistenceQuery, QueryResponse<ExistenceResponse>>
{
    private readonly LegacyContext _legacyContext;

    public CheckCredentialExistenceHandler(IDataLayer dataLayer, LegacyContext legacyContext)
    {
        _legacyContext = legacyContext;
        _dataLayer = dataLayer;
    }
    public async Task<QueryResponse<ExistenceResponse>> Handle(CheckCredentialExistenceQuery request, CancellationToken cancellationToken)
    {
        
        if (string.IsNullOrEmpty(request.Password))
        {
            return new ()
            {
                Message = $"The password is required",
                HttpStatusCode = HttpStatusCode.BadRequest
            };
        }

        if (request.Password.Length < 8)
        {
            return new ()
            {
                Message = $"The password length must be greater than 8 characters",
                HttpStatusCode = HttpStatusCode.BadRequest
            };
        }
        
        // Validate Password
        /*var passwordIsStrong = Regex.Match(request.Password, @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^\da-zA-Z]).{8,}$").Success;
        if (!passwordIsStrong)
        {
            return new ()
            {
                Message = $"The password is weak. Please include at least one of the following: 1 upper case, 1 lower case, a number and a special character",
                HttpStatusCode = HttpStatusCode.BadRequest
            };
        }*/

        if (string.IsNullOrEmpty(request.UserName))
        {
            return new ()
            {
                HttpStatusCode = HttpStatusCode.Accepted
            };
        }
        
        var existing = _dataLayer.TblIdentityCredentials
            .AsNoTracking()
            .Where(i => i.UserName == request.UserName)
            .Where(i => i.Guid != $"{request.Guid}")
            .Any();
            
        if (existing)
        {
            return new ()
            {
                Message = $"The identity with username '{request.UserName}' already exists",
                HttpStatusCode = HttpStatusCode.Conflict
            };
        }
      

        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted
        };
    }
}