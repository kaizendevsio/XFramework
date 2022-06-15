using HealthEssentials.Core.DataAccess.Commands.Entity.Laboratory;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Laboratory;

public class UpdateLaboratoryMemberHandler : CommandBaseHandler, IRequestHandler<UpdateLaboratoryMemberCmd, CmdResponse<UpdateLaboratoryMemberCmd>>
{
    public UpdateLaboratoryMemberHandler()
    {
        
    }
    public async Task<CmdResponse<UpdateLaboratoryMemberCmd>> Handle(UpdateLaboratoryMemberCmd request, CancellationToken cancellationToken)
    {
        var existingLaboratoryMember = await _dataLayer.HealthEssentialsContext.LaboratoryMembers
            .FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        if (existingLaboratoryMember == null)
        {
            return new()
            {
                Message = $"Laboratory member with Guid {request.Guid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }

        
        
        
        
        
        var updatedLaboratoryMember = request.Adapt(existingLaboratoryMember);
        updatedLaboratoryMember.Guid = request.Guid is null ? $"{Guid.NewGuid()}" : $"{request.Guid}";
        /*updatedRecord.Laboratory = laboratory;*/
        
        _dataLayer.HealthEssentialsContext.LaboratoryMembers.Update(updatedLaboratoryMember);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);

        return new()
        {
            Message = $"Laboratory member with Guid {updatedLaboratoryMember.Guid} updated successfully",
            HttpStatusCode = HttpStatusCode.Accepted,
            IsSuccess = true,
            Request = new()
            {
                Guid = Guid.Parse(updatedLaboratoryMember.Guid)
            }
        };


    }
}