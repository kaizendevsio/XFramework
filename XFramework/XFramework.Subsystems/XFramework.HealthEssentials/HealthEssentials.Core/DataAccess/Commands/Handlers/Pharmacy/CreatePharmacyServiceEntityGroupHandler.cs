using HealthEssentials.Core.DataAccess.Commands.Entity.Pharmacy;
using HealthEssentials.Domain.DataTransferObjects.XnelSystemsHealthEssentials;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Pharmacy;

public class CreatePharmacyServiceEntityGroupHandler : CommandBaseHandler, IRequestHandler<CreatePharmacyServiceEntityGroupCmd, CmdResponse<CreatePharmacyServiceEntityGroupCmd>>
{
    public CreatePharmacyServiceEntityGroupHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<CreatePharmacyServiceEntityGroupCmd>> Handle(CreatePharmacyServiceEntityGroupCmd request, CancellationToken cancellationToken)
    {
        var group = request.Adapt<PharmacyServiceEntityGroup>();
        group.Guid = request.Guid is null ? $"{Guid.NewGuid()}" : $"{request.Guid}";
        
        await _dataLayer.HealthEssentialsContext.PharmacyServiceEntityGroups.AddAsync(group, CancellationToken.None);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        request.Guid = Guid.Parse(group.Guid);
        return new()
        {
            Message = $"Pharmacy Service Entity Group with Guid {group.Guid} created successfully",
            HttpStatusCode = HttpStatusCode.Accepted,
            IsSuccess = true,
        };
    }
}