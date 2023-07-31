using HealthEssentials.Core.DataAccess.Commands.Entity.Pharmacy;
using HealthEssentials.Domain.DataTransferObjects.XnelSystemsHealthEssentials;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Pharmacy;

public class CreatePharmacyServiceHandler : CommandBaseHandler, IRequestHandler<CreatePharmacyServiceCmd, CmdResponse<CreatePharmacyServiceCmd>>
{
    public CreatePharmacyServiceHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<CreatePharmacyServiceCmd>> Handle(CreatePharmacyServiceCmd request, CancellationToken cancellationToken)
    {
        var entity = await _dataLayer.HealthEssentialsContext.PharmacyServiceEntities.FirstOrDefaultAsync(x => x.Guid == $"{request.EntityGuid}", CancellationToken.None);
        if (entity is null)
        {
            return new ()
            {
                Message = $"Pharmacy Service Entity with Guid {request.EntityGuid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }

        var location = await _dataLayer.HealthEssentialsContext.PharmacyLocations.FirstOrDefaultAsync(x => x.Guid == $"{request.PharmacyLocationGuid}", CancellationToken.None);
        if (location is null)
        {
            return new ()
            {
                Message = $"Pharmacy Location Entity with Guid {request.PharmacyLocationGuid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }

        var pharmacy = await _dataLayer.HealthEssentialsContext.Pharmacies.FirstOrDefaultAsync(x => x.Guid == $"{request.PharmacyGuid}", CancellationToken.None);
        if (pharmacy is null)
        {
            return new ()
            {
                Message = $"Pharmacy Entity with Guid {request.PharmacyGuid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        var unit = await _dataLayer.HealthEssentialsContext.Units.FirstOrDefaultAsync(x => x.Guid == $"{request.UnitGuid}", CancellationToken.None);
        if (unit is null)
        {
            return new ()
            {
                Message = $"Unit Entity with Guid {request.UnitGuid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }

        var service = request.Adapt<PharmacyService>();
        service.Guid = request.Guid is null ? $"{Guid.NewGuid()}" : $"{request.Guid}";
        service.PharmacyLocation = location;
        service.Pharmacy = pharmacy;
        service.Unit = unit;
        service.Entity = entity;
        
        await _dataLayer.HealthEssentialsContext.PharmacyServices.AddAsync(service, CancellationToken.None);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        request.Guid = Guid.Parse(service.Guid);
        return new()
        {
            Message = $"Pharmacy Service with Guid {service.Guid} created successfully",
            HttpStatusCode = HttpStatusCode.Accepted,
            
        };
    }
}