using HealthEssentials.Core.DataAccess.Commands.Entity.Pharmacy;
using TypeSupport.Extensions;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Pharmacy;

public class UpdatePharmacyServiceHandler : CommandBaseHandler, IRequestHandler<UpdatePharmacyServiceCmd, CmdResponse<UpdatePharmacyServiceCmd>>
{
    public UpdatePharmacyServiceHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<UpdatePharmacyServiceCmd>> Handle(UpdatePharmacyServiceCmd request, CancellationToken cancellationToken)
    {
        var existingService = await _dataLayer.HealthEssentialsContext.PharmacyServices.FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        if (existingService is null)
        {
            return new ()
            {
                Message = $"Pharmacy Service with Guid {request.Guid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        var updatedService = request.Adapt(existingService);

        if (request.EntityGuid is null)
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
            updatedService.Type = entity;
        }

        if (request.PharmacyLocationGuid is null)
        {
            var location = await _dataLayer.HealthEssentialsContext.PharmacyLocations.FirstOrDefaultAsync(x => x.Guid == $"{request.PharmacyLocationGuid}", CancellationToken.None);
            if (location is null)
            {
                return new ()
                {
                    Message = $"Pharmacy Location Entity with Guid {request.PharmacyLocationGuid} does not exist",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }
            updatedService.PharmacyLocation = location;
        }

        if (request.PharmacyGuid is null)
        {
            var pharmacy = await _dataLayer.HealthEssentialsContext.Pharmacies.FirstOrDefaultAsync(x => x.Guid == $"{request.PharmacyGuid}", CancellationToken.None);
            if (pharmacy is null)
            {
                return new ()
                {
                    Message = $"Pharmacy Entity with Guid {request.PharmacyGuid} does not exist",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }
            updatedService.Pharmacy = pharmacy;
        }

        if (request.UnitGuid is null)
        {
            var unit = await _dataLayer.HealthEssentialsContext.Units.FirstOrDefaultAsync(x => x.Guid == $"{request.UnitGuid}", CancellationToken.None);
            if (unit is null)
            {
                return new ()
                {
                    Message = $"Unit Entity with Guid {request.UnitGuid} does not exist",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }
            updatedService.Unit = unit;
        }

        _dataLayer.HealthEssentialsContext.Update(updatedService);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        return new ()
        {
            Message = $"Pharmacy Service with Guid {request.Guid} updated successfully",
            HttpStatusCode = HttpStatusCode.Accepted
        };
    }
}