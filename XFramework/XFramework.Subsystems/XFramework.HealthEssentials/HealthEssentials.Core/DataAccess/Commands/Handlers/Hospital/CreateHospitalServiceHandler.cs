using HealthEssentials.Core.DataAccess.Commands.Entity.Hospital;
using HealthEssentials.Domain.DataTransferObjects.XnelSystemsHealthEssentials;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Hospital;

public class CreateHospitalServiceHandler : CommandBaseHandler, IRequestHandler<CreateHospitalServiceCmd, CmdResponse<CreateHospitalServiceCmd>>
{
    public CreateHospitalServiceHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<CreateHospitalServiceCmd>> Handle(CreateHospitalServiceCmd request, CancellationToken cancellationToken)
    {
        var entity = await _dataLayer.HealthEssentialsContext.HospitalServiceEntities.FirstOrDefaultAsync(x => x.Guid == $"{request.EntityGuid}", CancellationToken.None);
        if (entity is null)
        {
            return new ()
            {
                Message = $"Hospital Service with Guid {request.EntityGuid} not found",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        var hospital = await _dataLayer.HealthEssentialsContext.Hospitals.FirstOrDefaultAsync(x => x.Guid == $"{request.HospitalGuid}", CancellationToken.None);
        if (hospital is null)
        {
            return new ()
            {
                Message = $"Hospital with Guid {request.HospitalGuid} not found",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        var hospitalLocation = await _dataLayer.HealthEssentialsContext.HospitalLocations.FirstOrDefaultAsync(x => x.Guid == $"{request.HospitalLocationGuid}", CancellationToken.None);
        if (hospitalLocation is null)
        {
            return new ()
            {
                Message = $"Hospital Location with Guid {request.HospitalLocationGuid} not found",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        var unit = await _dataLayer.HealthEssentialsContext.Units.FirstOrDefaultAsync(x => x.Guid == $"{request.UnitGuid}", CancellationToken.None);
        if (unit is null)
        {
            return new ()
            {
                Message = $"Unit with Guid {request.UnitGuid} not found",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }

        var service = request.Adapt<HospitalService>();
        service.Guid = request.Guid is null ? $"{Guid.NewGuid()}" : $"{request.Guid}";
        service.Hospital = hospital;
        service.HospitalLocation = hospitalLocation;
        service.Unit = unit;
        service.Entity = entity;
        
        await _dataLayer.HealthEssentialsContext.HospitalServices.AddAsync(service, CancellationToken.None);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        request.Guid = Guid.Parse(service.Guid);
        return new()
        {
            Message = $"Hospital Service {service.Guid} created successfully",
            HttpStatusCode = HttpStatusCode.Accepted,
            
        };


    }
}