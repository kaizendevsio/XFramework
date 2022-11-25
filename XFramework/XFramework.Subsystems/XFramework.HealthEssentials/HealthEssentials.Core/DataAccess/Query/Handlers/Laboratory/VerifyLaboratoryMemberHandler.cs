using HealthEssentials.Core.DataAccess.Query.Entity.Doctor;
using HealthEssentials.Core.DataAccess.Query.Entity.Laboratory;
using HealthEssentials.Domain.Generics.Contracts.Responses.Common;
using XFramework.Domain.Generic.Enums;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Laboratory;

public class VerifyLaboratoryMemberHandler : QueryBaseHandler, IRequestHandler<VerifyLaboratoryMemberQuery, QueryResponse<IdentityValidationResponse>>
{
    public VerifyLaboratoryMemberHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<QueryResponse<IdentityValidationResponse>> Handle(VerifyLaboratoryMemberQuery request, CancellationToken cancellationToken)
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

        var identity = await _dataLayer.HealthEssentialsContext.LaboratoryMembers.FirstOrDefaultAsync(i => i.CredentialGuid == credential.Guid, CancellationToken.None);
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
                IsActivated = identity.Status is (int)GenericStatusType.Approved,
                Guid = Guid.Parse(identity.Guid)
            },
            HttpStatusCode = HttpStatusCode.Accepted,
            IsSuccess = true
        };
    }
}