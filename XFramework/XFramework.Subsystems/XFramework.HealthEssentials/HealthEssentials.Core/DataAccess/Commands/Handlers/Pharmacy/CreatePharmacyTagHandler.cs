using HealthEssentials.Core.DataAccess.Commands.Entity.Pharmacy;
using HealthEssentials.Domain.Generics.Enums;
using PharmacyTag = HealthEssentials.Domain.Generics.Contracts.PharmacyTag;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Pharmacy;

public class CreatePharmacyTagHandler : CommandBaseHandler, IRequestHandler<CreatePharmacyTagCmd, CmdResponse<CreatePharmacyTagCmd>>
{
    public CreatePharmacyTagHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<CreatePharmacyTagCmd>> Handle(CreatePharmacyTagCmd request, CancellationToken cancellationToken)
    {
        var pharmacy = await _dataLayer.HealthEssentialsContext.Pharmacies.FirstOrDefaultAsync(x => x.Guid == $"{request.PharmacyGuid}", CancellationToken.None);
        if (pharmacy is null)
        {
            return new ()
            {
                Message = $"Pharmacy with Guid {request.PharmacyGuid} does not exist",
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

        var pharmacyTag = request.Adapt<PharmacyTag>();
        pharmacyTag.Guid = request.Guid is null ? $"{Guid.NewGuid()}" : $"{request.Guid}";
        pharmacyTag.Pharmacy = pharmacy;
        pharmacyTag.Tag = tag;
        
        await _dataLayer.HealthEssentialsContext.PharmacyTags.AddAsync(pharmacyTag, CancellationToken.None);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        request.Guid = Guid.Parse(pharmacyTag.Guid);
        return new()
        {
            Message = $"Pharmacy Tag with Guid {pharmacyTag.Guid} created successfully",
            HttpStatusCode = HttpStatusCode.Accepted,
            
        };
    }
}