using HealthEssentials.Core.DataAccess.Commands.Entity.Pharmacy;
using HealthEssentials.Domain.DataTransferObjects.XnelSystemsHealthEssentials;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Pharmacy;

public class CreatePharmacyServiceTagHandler : CommandBaseHandler, IRequestHandler<CreatePharmacyServiceTagCmd, CmdResponse<CreatePharmacyServiceTagCmd>>
{
    public CreatePharmacyServiceTagHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<CreatePharmacyServiceTagCmd>> Handle(CreatePharmacyServiceTagCmd request, CancellationToken cancellationToken)
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
        
        var tag = await _dataLayer.HealthEssentialsContext.Tags.FirstOrDefaultAsync(x => x.Guid == $"{request.TagGuid}", CancellationToken.None);
        if (tag is null)
        {
            return new ()
            {
                Message = $"Tag with Guid {request.TagGuid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }

        var serviceTag = request.Adapt<PharmacyServiceTag>();
        serviceTag.Guid = request.Guid is null ? $"{Guid.NewGuid()}" : $"{request.Guid}";
        serviceTag.PharmacyService = service;
        serviceTag.Tag = tag;
        
        await _dataLayer.HealthEssentialsContext.PharmacyServiceTags.AddAsync(serviceTag, CancellationToken.None);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        request.Guid = Guid.Parse(serviceTag.Guid);
        return new()
        {
            Message = $"Pharmacy Service Tag with Guid {serviceTag.Guid} created successfully",
            HttpStatusCode = HttpStatusCode.Accepted,
            
        };
    }
}