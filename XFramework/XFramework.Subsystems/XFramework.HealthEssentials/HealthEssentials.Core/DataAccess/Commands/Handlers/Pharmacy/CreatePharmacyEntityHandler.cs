using HealthEssentials.Core.DataAccess.Commands.Entity.Pharmacy;
using HealthEssentials.Domain.DataTransferObjects.XnelSystemsHealthEssentials;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Pharmacy;

public class CreatePharmacyEntityHandler : CommandBaseHandler, IRequestHandler<CreatePharmacyEntityCmd, CmdResponse<CreatePharmacyEntityCmd>>
{
    public CreatePharmacyEntityHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<CreatePharmacyEntityCmd>> Handle(CreatePharmacyEntityCmd request, CancellationToken cancellationToken)
    {
        var entity = request.Adapt<PharmacyEntity>();
        entity.Guid = request.Guid is null ? $"{Guid.NewGuid()}" : $"{request.Guid}";
        
        await _dataLayer.HealthEssentialsContext.PharmacyEntities.AddAsync(entity, CancellationToken.None);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        request.Guid = Guid.Parse(entity.Guid);
        return new()
        {
            Message = $"Pharmacy Entity with Id {entity.Guid} created successfully",
            HttpStatusCode = HttpStatusCode.Accepted,
            IsSuccess = true,
        };
    }
}