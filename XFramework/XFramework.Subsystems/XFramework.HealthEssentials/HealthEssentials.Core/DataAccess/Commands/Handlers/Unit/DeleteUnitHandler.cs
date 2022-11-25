using HealthEssentials.Core.DataAccess.Commands.Entity.Unit;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Unit;

public class DeleteUnitHandler : CommandBaseHandler, IRequestHandler<DeleteUnitCmd, CmdResponse<DeleteUnitCmd>>
{
    public DeleteUnitHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<DeleteUnitCmd>> Handle(DeleteUnitCmd request, CancellationToken cancellationToken)
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
        
        existingUnit.IsDeleted = true;
        existingUnit.IsEnabled = false;

        _dataLayer.HealthEssentialsContext.Update(existingUnit);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        return new()
        {
            Message = $"Unit with Guid {request.Guid} deleted successfully",
            HttpStatusCode = HttpStatusCode.OK
        };
    }
}