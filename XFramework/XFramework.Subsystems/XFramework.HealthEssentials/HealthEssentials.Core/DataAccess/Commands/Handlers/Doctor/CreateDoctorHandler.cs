using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using HealthEssentials.Core.DataAccess.Commands.Entity.Doctor;
using HealthEssentials.Domain.DataTransferObjects;
using HealthEssentials.Domain.DataTransferObjects.XnelSystemsHealthEssentials;
using XFramework.Domain.Generic.Enums;
using XFramework.Integration.Interfaces;
using XFramework.Integration.Services.Helpers;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Doctor;

public class CreateDoctorHandler : CommandBaseHandler, IRequestHandler<CreateDoctorCmd, CmdResponse<CreateDoctorCmd>>
{
    private readonly IHelperService _helperService;

    public CreateDoctorHandler(IDataLayer dataLayer, IHelperService helperService)
    {
        _helperService = helperService;
        _dataLayer = dataLayer;
    }
    public async Task<CmdResponse<CreateDoctorCmd>> Handle(CreateDoctorCmd request, CancellationToken cancellationToken)
    {
        var credential = await _dataLayer.XnelSystemsContext.IdentityCredentials.FirstOrDefaultAsync(i => i.Guid == $"{request.CredentialGuid}", CancellationToken.None);
        if (credential is null)
        {
            return new ()
            {
                Message = $"Credential with Guid {request.CredentialGuid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        var entity = await _dataLayer.HealthEssentialsContext.DoctorEntities.FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        if (entity is null)
        {
            return new ()
            {
                Message = $"Doctor with Guid {request.Guid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }

        var doctor = request.Adapt<Domain.DataTransferObjects.XnelSystemsHealthEssentials.Doctor>();
        doctor.Guid = request.Guid is null ? $"{Guid.NewGuid()}" : $"{request.Guid}";
        doctor.Entity = entity;
        doctor.CredentialId = credential.Id;
        
        await _dataLayer.HealthEssentialsContext.Doctors.AddAsync(doctor, CancellationToken.None);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);

        request.Guid = Guid.Parse(doctor.Guid);
        return new()
        {
            Message = $"Doctor {request.ProfessionalName} created successfully",
            HttpStatusCode = HttpStatusCode.Accepted,
            IsSuccess = true,
        };
    }
}