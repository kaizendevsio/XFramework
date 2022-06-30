using HealthEssentials.Core.DataAccess.Commands.Entity.Laboratory;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Laboratory;

public class DeleteLaboratoryHandler : CommandBaseHandler, IRequestHandler<DeleteLaboratoryCmd, CmdResponse<DeleteLaboratoryCmd>>
{
    public DeleteLaboratoryHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<DeleteLaboratoryCmd>> Handle(DeleteLaboratoryCmd request, CancellationToken cancellationToken)
    {
        var existingLaboratory = await _dataLayer.HealthEssentialsContext.Laboratories
            .FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);

        if (existingLaboratory == null)
        {
            return new()
            {
                Message = $"Laboratory with Guid {request.Guid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        existingLaboratory.IsDeleted = true;
        existingLaboratory.IsEnabled = false;

        _dataLayer.HealthEssentialsContext.Update(existingLaboratory);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);

        return new()
        {
            Message = $"Laboratory with Guid {request.Guid} has been deleted",
            HttpStatusCode = HttpStatusCode.Accepted,
            IsSuccess = true
        };
    }
}