using HealthEssentials.Core.DataAccess.Commands.Entity.Pharmacy;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Pharmacy;

public class DeletePharmacyServiceTagHandler : CommandBaseHandler, IRequestHandler<DeletePharmacyServiceTagCmd, CmdResponse<DeletePharmacyServiceTagCmd>>
{
    public DeletePharmacyServiceTagHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<DeletePharmacyServiceTagCmd>> Handle(DeletePharmacyServiceTagCmd request, CancellationToken cancellationToken)
    {
        var existingServiceTag = await _dataLayer.HealthEssentialsContext.PharmacyServiceTags.FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        if (existingServiceTag is null)
        {
            return new ()
            {
                Message = $"Pharmacy Service Tag with Guid {request.Guid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        existingServiceTag.IsDeleted = true;
        existingServiceTag.IsEnabled = false;

        _dataLayer.HealthEssentialsContext.Update(existingServiceTag);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        return new ()
        {
            Message = $"Pharmacy Service Tag with Guid {request.Guid} has been deleted",
            HttpStatusCode = HttpStatusCode.OK
        };
    }
}