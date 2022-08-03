using HealthEssentials.Core.DataAccess.Commands.Entity.Unit;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Unit;

public class UpdateUnitHandler : CommandBaseHandler, IRequestHandler<UpdateUnitCmd, CmdResponse<UpdateUnitCmd>>
{
    public UpdateUnitHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<UpdateUnitCmd>> Handle(UpdateUnitCmd request, CancellationToken cancellationToken)
    {
        var existingUnit = await _dataLayer.HealthEssentialsContext.Units.FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        if (existingUnit == null)
        {
            return new()
            {
                Message = $"Unit with Guid {request.Guid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        var updatedUnit = request.Adapt(existingUnit);

        if (request.EntityGuid is null)
        {
            var entity = await _dataLayer.HealthEssentialsContext.UnitEntities.FirstOrDefaultAsync(x => x.Guid == $"{request.EntityGuid}", CancellationToken.None);
            if (entity is null)
            {
                return new ()
                {
                    Message = $"Unit with Guid {request.EntityGuid} does not exist",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }
            updatedUnit.Entity = entity;
        }

        _dataLayer.HealthEssentialsContext.Update(updatedUnit);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        return new()
        {
            Message = $"Unit with Guid {request.Guid} updated successfully",
            HttpStatusCode = HttpStatusCode.OK
        };
    }
}