using HealthEssentials.Core.DataAccess.Commands.Entity.Patient;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Patient;

public class CreatePatientHandler : CommandBaseHandler, IRequestHandler<CreatePatientCmd, CmdResponse<CreatePatientCmd>>
{
    public CreatePatientHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<CreatePatientCmd>> Handle(CreatePatientCmd request, CancellationToken cancellationToken)
    {
        var credential = await _dataLayer.XnelSystemsContext.IdentityCredentials
            .FirstOrDefaultAsync(i => i.Guid == $"{request.CredentialGuid}", cancellationToken: cancellationToken);
       
        if (credential is null)
        {
            return new ()
            {
                Message = $"Credential with Guid {request.CredentialGuid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }

        var entity = await _dataLayer.HealthEssentialsContext.PatientEntities
            .FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);

        if (entity is null)
        {
            return new ()
            {
                Message = $"Entity with Guid {request.Guid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }

        var patient = request.Adapt<Domain.DataTransferObjects.XnelSystemsHealthEssentials.Patient>();
        patient.Guid = request.Guid is null ? $"{Guid.NewGuid()}" : $"{request.Guid}";
        patient.Entity = entity;
        patient.CredentialId = credential.Id;
        
        await _dataLayer.HealthEssentialsContext.Patients.AddAsync(patient,CancellationToken.None);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);

        return new()
        {
            Message = $"Patient with Guid {patient.Guid} created successfully",
            HttpStatusCode = HttpStatusCode.Accepted,
            IsSuccess = true,
            Request = new()
            {
                Guid = Guid.Parse(patient.Guid)
            }
        };
    }
}