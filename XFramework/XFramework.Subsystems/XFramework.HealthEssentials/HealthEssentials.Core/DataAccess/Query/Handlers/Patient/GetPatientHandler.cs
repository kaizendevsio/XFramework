using HealthEssentials.Core.DataAccess.Query.Entity.Patient;
using HealthEssentials.Domain.Generics.Contracts.Responses.Patient;
using IdentityServer.Domain.Generic.Contracts.Responses;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Patient;

public class GetPatientHandler : QueryBaseHandler, IRequestHandler<GetPatientQuery, QueryResponse<PatientResponse>>
{
    public GetPatientHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<QueryResponse<PatientResponse>> Handle(GetPatientQuery request, CancellationToken cancellationToken)
    {
        var credential = await _dataLayer.XnelSystemsContext.IdentityCredentials
            .Include(i => i.IdentityInfo)
            .Include(i => i.IdentityContacts)
            .ThenInclude(i => i.Entity)
            .AsNoTracking()
            .AsSplitQuery()
            .FirstOrDefaultAsync(i => i.Guid == $"{request.CredentialGuid}", cancellationToken: cancellationToken);
       
        if (credential is null)
        {
            return new ()
            {
                Message = $"Credential with Guid {request.CredentialGuid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }

        var identity = await _dataLayer.HealthEssentialsContext.Patients.FirstOrDefaultAsync(i => i.CredentialId == credential.Guid, CancellationToken.None);
        if (identity is null)
        {
            return new ()
            {
                HttpStatusCode = HttpStatusCode.NotFound,
                IsSuccess = false,
                Message = $"Patient with CredentialId {credential.Id} does not exist"
            };
        }

        var response = identity.Adapt<PatientResponse>();
        response.Credential = credential.Adapt<CredentialResponse>();
        return new ()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            IsSuccess = true,
            Response = response
        };
    }
}