using HealthEssentials.Core.DataAccess.Commands.Entity.Laboratory;
using HealthEssentials.Core.DataAccess.Commands.Entity.Pharmacy;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Pharmacy;

public class UpdatePharmacyMemberHandler : CommandBaseHandler, IRequestHandler<UpdatePharmacyMemberCmd, CmdResponse<UpdatePharmacyMemberCmd>>
{
    public UpdatePharmacyMemberHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<UpdatePharmacyMemberCmd>> Handle(UpdatePharmacyMemberCmd request, CancellationToken cancellationToken)
    {
        var pharmacyMember = await _dataLayer.HealthEssentialsContext.PharmacyMembers.FirstOrDefaultAsync(i => i.Guid == $"{request.Guid}", cancellationToken: cancellationToken);
        if (pharmacyMember is null)
        {
            return new ()
            {
                Message = $"Pharmacy member with Guid {request.Guid} not found",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        pharmacyMember.Status = (int) request.Status;
        _dataLayer.HealthEssentialsContext.Update(pharmacyMember);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        return new()
        {
            Message = "Laboratory updated successfully",
            HttpStatusCode = HttpStatusCode.Accepted,
            Request = request
        };
    }
}