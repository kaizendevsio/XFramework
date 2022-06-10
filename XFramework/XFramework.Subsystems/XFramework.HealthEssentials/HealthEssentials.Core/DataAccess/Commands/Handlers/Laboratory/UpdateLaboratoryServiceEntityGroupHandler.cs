using HealthEssentials.Core.DataAccess.Commands.Entity.Laboratory;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Laboratory;

public class UpdateLaboratoryServiceEntityGroupHandler : CommandBaseHandler, IRequestHandler<UpdateLaboratoryServiceEntityGroupCmd, CmdResponse<UpdateLaboratoryServiceEntityGroupCmd>>
{
    public UpdateLaboratoryServiceEntityGroupHandler()
    {
        
    }
    public async Task<CmdResponse<UpdateLaboratoryServiceEntityGroupCmd>> Handle(UpdateLaboratoryServiceEntityGroupCmd request, CancellationToken cancellationToken)
    {
        var existingRecord = await _dataLayer.HealthEssentialsContext.LaboratoryServiceEntityGroups
            .FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);

        if (existingRecord == null)
        {
            return new()
            {
                Message = "Laboratory service group with Guid ${request.Guid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        var serviceEntityGroup = request.Adapt<Domain.DataTransferObjects.XnelSystemsHealthEssentials.LaboratoryServiceEntityGroup>();
        serviceEntityGroup.Guid = request.Guid is null ? $"{Guid.NewGuid()}" : $"{request.Guid}";

        var updatedRecord = request.Adapt(existingRecord);
        updatedRecord.Guid = request.Guid is null ? $"{Guid.NewGuid()}" : $"{request.Guid}";
        
        _dataLayer.HealthEssentialsContext.LaboratoryServiceEntityGroups.Update(serviceEntityGroup);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);

        return new()
        {
            Message = $"Laboratory service type group with Guid {updatedRecord.Guid} updated successfully",
            HttpStatusCode = HttpStatusCode.Accepted,
            IsSuccess = true,
            Request = new()
            {
                Guid = Guid.Parse(updatedRecord.Guid)
            }
        };
    }
}