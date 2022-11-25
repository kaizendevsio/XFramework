using HealthEssentials.Core.DataAccess.Commands.Entity.Unit;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Unit;

public class CreateUnitHandler : CommandBaseHandler, IRequestHandler<CreateUnitCmd, CmdResponse<CreateUnitCmd>>
{
    public CreateUnitHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<CreateUnitCmd>> Handle(CreateUnitCmd request, CancellationToken cancellationToken)
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

        var unit = request.Adapt<Domain.DataTransferObjects.XnelSystemsHealthEssentials.Unit>();
        unit.Guid = request.Guid is null ? $"{Guid.NewGuid()}" : $"{request.Guid}";
        unit.Entity = entity;

        await _dataLayer.HealthEssentialsContext.Units.AddAsync(unit, CancellationToken.None);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        request.Guid = Guid.Parse(unit.Guid);
        return new()
        {
            Message = $"Unit with Guid {unit.Guid} created successfully",
            HttpStatusCode = HttpStatusCode.Accepted,
            IsSuccess = true,
        }; 
    }
}