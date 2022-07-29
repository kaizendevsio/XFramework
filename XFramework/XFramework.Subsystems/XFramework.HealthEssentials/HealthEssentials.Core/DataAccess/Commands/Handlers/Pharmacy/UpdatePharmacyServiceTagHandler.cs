using HealthEssentials.Core.DataAccess.Commands.Entity.Pharmacy;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Pharmacy;

public class UpdatePharmacyServiceTagHandler : CommandBaseHandler, IRequestHandler<UpdatePharmacyServiceTagCmd, CmdResponse<UpdatePharmacyServiceTagCmd>>
{
    public UpdatePharmacyServiceTagHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<UpdatePharmacyServiceTagCmd>> Handle(UpdatePharmacyServiceTagCmd request, CancellationToken cancellationToken)
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
        var updatedServiceTag = request.Adapt(existingServiceTag);

        if (request.PharmacyServiceGuid is null)
        {
            var service = await _dataLayer.HealthEssentialsContext.PharmacyServices.FirstOrDefaultAsync(x => x.Guid == $"{request.PharmacyServiceGuid}", CancellationToken.None);
            if (service is null)
            {
                return new ()
                {
                    Message = $"Pharmacy Service with Guid {request.PharmacyServiceGuid} does not exist",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }
            updatedServiceTag.PharmacyService = service;
        }

        if (request.TagGuid is null)
        {
            var tag = await _dataLayer.HealthEssentialsContext.Tags.FirstOrDefaultAsync(x => x.Guid == $"{request.TagGuid}", CancellationToken.None);
            if (tag is null)
            {
                return new ()
                {
                    Message = $"Tag with Guid {request.TagGuid} does not exist",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }
            updatedServiceTag.Tag = tag;
        }

        _dataLayer.HealthEssentialsContext.Update(updatedServiceTag);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        return new ()
        {
            Message = $"Pharmacy Service Tag with Guid {request.Guid} updated successfully",
            HttpStatusCode = HttpStatusCode.OK
        };

    }
}