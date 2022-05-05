using HealthEssentials.Core.DataAccess.Commands.Entity.Patient;
using HealthEssentials.Core.Interfaces;
using Mapster;
using Microsoft.EntityFrameworkCore;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Patient;

public class CreatePatientIdentityHandler : CommandBaseHandler, IRequestHandler<CreatePatientIdentityCmd, CmdResponse<CreatePatientIdentityCmd>>
{
    public CreatePatientIdentityHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<CreatePatientIdentityCmd>> Handle(CreatePatientIdentityCmd request, CancellationToken cancellationToken)
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
        
        var entity = request.Adapt<Domain.DataTransferObjects.XnelSystemsHealthEssentials.Patient>();
        entity.Guid = request.Guid is null ? $"{Guid.NewGuid()}" : $"{request.Guid}";
        entity.EntityId = 1;
        
        _dataLayer.HealthEssentialsContext.Patients.Add(entity);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);

        return new()
        {
            Message = $"Patient with Guid {entity.Guid} created successfully",
            HttpStatusCode = HttpStatusCode.Accepted,
            IsSuccess = true,
            Request = new()
            {
                Guid = Guid.Parse(entity.Guid)
            }
        };
    }
}