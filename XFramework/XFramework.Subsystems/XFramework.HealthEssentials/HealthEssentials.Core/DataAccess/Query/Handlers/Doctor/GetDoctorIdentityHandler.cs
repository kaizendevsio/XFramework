using HealthEssentials.Core.DataAccess.Query.Entity.Doctor;
using HealthEssentials.Domain.Generics.Contracts.Responses.Common;
using HealthEssentials.Domain.Generics.Contracts.Responses.Doctor;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Doctor;

public class GetDoctorIdentityHandler : QueryBaseHandler, IRequestHandler<GetDoctorIdentityQuery, QueryResponse<DoctorResponse>>
{
    public GetDoctorIdentityHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
        MediatRGuid = Guid.NewGuid();
    }

    public Guid? MediatRGuid { get; set; }
    public async Task<QueryResponse<DoctorResponse>> Handle(GetDoctorIdentityQuery request, CancellationToken cancellationToken)
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

        var identity = await _dataLayer.HealthEssentialsContext.Doctors
            .AsNoTracking()
            .Include(i => i.Entity)
            .FirstOrDefaultAsync(i => i.CredentialId == credential.Id, CancellationToken.None);
        if (identity is null)
        {
            return new ()
            {
                HttpStatusCode = HttpStatusCode.Accepted,
                Message = $"Doctor with CredentialId {credential.Id} does not exist",
                IsSuccess = true
            };
        }
        
        return new ()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Response = identity.Adapt<DoctorResponse>(),
            IsSuccess = true
        };
    }
}