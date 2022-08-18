using HealthEssentials.Core.DataAccess.Commands.Entity.Hospital;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Hospital;

public class CreateHospitalHandler : CommandBaseHandler, IRequestHandler<CreateHospitalCmd, CmdResponse<CreateHospitalCmd>>
{
    public CreateHospitalHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<CreateHospitalCmd>> Handle(CreateHospitalCmd request, CancellationToken cancellationToken)
    {
        var entity = await _dataLayer.HealthEssentialsContext.HospitalEntities.FirstOrDefaultAsync(x => x.Guid == $"{request.EntityGuid}", CancellationToken.None);
        if (entity is null)
        {
            return new ()
            {
                Message = $"Hospital with Guid {request.EntityGuid} not found",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }

        var hospital = request.Adapt<Domain.DataTransferObjects.XnelSystemsHealthEssentials.Hospital>();
        hospital.Guid = request.Guid is null ? $"{Guid.NewGuid()}" : $"{request.Guid}";
        hospital.Entity = entity;
        
        await _dataLayer.HealthEssentialsContext.Hospitals.AddAsync(hospital, CancellationToken.None);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        request.Guid = Guid.Parse(hospital.Guid);
        return new()
        {
            Message = $"Hospital {hospital.Guid} created successfully",
            HttpStatusCode = HttpStatusCode.Accepted,
            IsSuccess = true,
        };
    }
}