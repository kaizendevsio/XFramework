using HealthEssentials.Core.DataAccess.Query.Entity.Patient;
using HealthEssentials.Core.Interfaces;
using HealthEssentials.Domain.Generics.Contracts.Responses.Patient;
using Mapster;
using Microsoft.EntityFrameworkCore;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Patient;

public class GetPatientIdentityHandler : QueryBaseHandler, IRequestHandler<GetPatientIdentityQuery, QueryResponse<PatientResponse>>
{
    public GetPatientIdentityHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<QueryResponse<PatientResponse>> Handle(GetPatientIdentityQuery request, CancellationToken cancellationToken)
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

        var identity = await _dataLayer.HealthEssentialsContext.Patients.FirstOrDefaultAsync(i => i.CredentialId == credential.Id, CancellationToken.None);
        if (identity is null)
        {
            return new ()
            {
                HttpStatusCode = HttpStatusCode.NotFound,
                IsSuccess = false,
                Message = $"Patient with CredentialId {credential.Id} does not exist"
            };
        }
        
        return new ()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            IsSuccess = true,
            Response = identity.Adapt<PatientResponse>()
        };
    }
}