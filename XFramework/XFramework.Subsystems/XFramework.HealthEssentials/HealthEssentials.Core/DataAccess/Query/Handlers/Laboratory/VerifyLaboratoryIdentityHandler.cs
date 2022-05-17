using HealthEssentials.Core.DataAccess.Query.Entity.Doctor;
using HealthEssentials.Core.DataAccess.Query.Entity.Laboratory;
using HealthEssentials.Domain.Generics.Contracts.Responses.Common;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Laboratory;

public class VerifyLaboratoryIdentityHandler : QueryBaseHandler, IRequestHandler<VerifyLaboratoryIdentityQuery, QueryResponse<IdentityValidationResponse>>
{
    public VerifyLaboratoryIdentityHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<QueryResponse<IdentityValidationResponse>> Handle(VerifyLaboratoryIdentityQuery request, CancellationToken cancellationToken)
    {
        var credential = await _dataLayer.XnelSystemsContext.IdentityCredentials
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Guid == $"{request.CredentialGuid}", cancellationToken: cancellationToken);
       
        if (credential is null)
        {
            return new ()
            {
                Message = $"Credential with Guid {request.CredentialGuid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }

        var identity = await _dataLayer.HealthEssentialsContext.LaboratoryMembers.FirstOrDefaultAsync(i => i.CredentialId == credential.Id, CancellationToken.None);
        if (identity is null)
        {
            return new ()
            {
                Response = new()
                {
                    IsExisting = false,
                    IsActivated = false
                },
                HttpStatusCode = HttpStatusCode.Accepted,
                IsSuccess = true
            };
        }
        
        return new ()
        {
            Response = new()
            {
                IsExisting = true,
                IsActivated = identity.IsEnabled
            },
            HttpStatusCode = HttpStatusCode.Accepted,
            IsSuccess = true
        };
    }
}