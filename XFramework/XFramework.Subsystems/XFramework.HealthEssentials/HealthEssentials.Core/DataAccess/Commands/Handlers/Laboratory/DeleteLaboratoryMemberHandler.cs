using HealthEssentials.Core.DataAccess.Commands.Entity.Laboratory;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Laboratory;

public class DeleteLaboratoryMemberHandler : CommandBaseHandler, IRequestHandler<DeleteLaboratoryMemberCmd, CmdResponse<DeleteLaboratoryMemberCmd>>
{
    public DeleteLaboratoryMemberHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<DeleteLaboratoryMemberCmd>> Handle(DeleteLaboratoryMemberCmd request, CancellationToken cancellationToken)
    {
        var existingLaboratoryMember = await _dataLayer.HealthEssentialsContext.LaboratoryMembers.FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        if (existingLaboratoryMember == null)
        {
            return new()
            {
                Message = $"Laboratory member with guid {request.Guid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        existingLaboratoryMember.IsDeleted = true;
        existingLaboratoryMember.IsEnabled = false;

        _dataLayer.HealthEssentialsContext.Update(existingLaboratoryMember);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);

        return new ()
        {
            Message = $"Laboratory member with guid {request.Guid} has been deleted",
            HttpStatusCode = HttpStatusCode.OK
        };
    }
}