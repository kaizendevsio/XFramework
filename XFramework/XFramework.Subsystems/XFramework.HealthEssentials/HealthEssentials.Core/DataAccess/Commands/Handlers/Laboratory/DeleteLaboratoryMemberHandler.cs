using HealthEssentials.Core.DataAccess.Commands.Entity.Laboratory;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Laboratory;

public class DeleteLaboratoryMemberHandler : CommandBaseHandler, IRequestHandler<DeleteLaboratoryMemberCmd, CmdResponse<DeleteLaboratoryMemberCmd>>
{
    public DeleteLaboratoryMemberHandler()
    {
        
    }
    public async Task<CmdResponse<DeleteLaboratoryMemberCmd>> Handle(DeleteLaboratoryMemberCmd request, CancellationToken cancellationToken)
    {
        var existingRecord = await _dataLayer.HealthEssentialsContext.LaboratoryMembers
            .FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        if (existingRecord == null)
        {
            return new()
            {
                Message = $"Laboratory member with Guid {request.Guid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        existingRecord.IsDeleted = true;
        existingRecord.IsEnabled = false;

        _dataLayer.HealthEssentialsContext.Update(existingRecord);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);

        return new()
        {
            Message = $"Laboratory member with Guid {request.Guid} has been deleted",
            HttpStatusCode = HttpStatusCode.Accepted,
            IsSuccess = true
        };
    }
}