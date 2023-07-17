using HealthEssentials.Core.DataAccess.Query.Entity.Doctor;
using HealthEssentials.Domain.Generics.Contracts.Responses.Common;
using XFramework.Domain.Generic.Enums;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Doctor;

public class VerifyDoctorHandler : QueryBaseHandler, IRequestHandler<VerifyDoctorQuery, QueryResponse<IdentityValidationResponse>>
{
    public VerifyDoctorHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
        MediatRGuid = Guid.NewGuid();
    }

    public Guid? MediatRGuid { get; set; }
    public async Task<QueryResponse<IdentityValidationResponse>> Handle(VerifyDoctorQuery request, CancellationToken cancellationToken)
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

        var identity = await _dataLayer.HealthEssentialsContext.Doctors.FirstOrDefaultAsync(i => i.CredentialGuid == credential.Guid, CancellationToken.None);
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
            
        };
    }
}