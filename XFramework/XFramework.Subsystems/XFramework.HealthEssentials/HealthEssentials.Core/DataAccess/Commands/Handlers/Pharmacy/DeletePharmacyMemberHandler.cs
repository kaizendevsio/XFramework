using HealthEssentials.Core.DataAccess.Commands.Entity.Pharmacy;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Pharmacy;

public class DeletePharmacyMemberHandler : CommandBaseHandler, IRequestHandler<DeletePharmacyMemberCmd, CmdResponse<DeletePharmacyMemberCmd>>
{
    public DeletePharmacyMemberHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<DeletePharmacyMemberCmd>> Handle(DeletePharmacyMemberCmd request, CancellationToken cancellationToken)
    {
        var existingPharmacyMember = await _dataLayer.HealthEssentialsContext.PharmacyMembers
            .FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);

        if (existingPharmacyMember == null)
        {
            return new()
            {
                Message = $"Pharmacy member with Guid {request.Guid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        existingPharmacyMember.IsDeleted = true;
        existingPharmacyMember.IsEnabled = false;

        _dataLayer.HealthEssentialsContext.Update(existingPharmacyMember);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        return new()
        {
            Message = $"Pharmacy member with Guid {request.Guid} has been deleted",
            HttpStatusCode = HttpStatusCode.Accepted,
            IsSuccess = true
        };
    }
}