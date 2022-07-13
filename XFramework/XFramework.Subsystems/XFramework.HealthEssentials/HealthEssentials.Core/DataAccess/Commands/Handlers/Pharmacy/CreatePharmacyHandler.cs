using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using HealthEssentials.Core.DataAccess.Commands.Entity.Logistic;
using HealthEssentials.Core.DataAccess.Commands.Entity.Pharmacy;
using HealthEssentials.Domain.DataTransferObjects.XnelSystemsHealthEssentials;
using XFramework.Domain.Generic.Enums;
using XFramework.Integration.Interfaces;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Pharmacy;

public class CreatePharmacyHandler : CommandBaseHandler, IRequestHandler<CreatePharmacyCmd, CmdResponse<CreatePharmacyCmd>>
{
    private readonly IHelperService _helperService;

    public CreatePharmacyHandler(IDataLayer dataLayer, IHelperService helperService)
    {
        _helperService = helperService;
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<CreatePharmacyCmd>> Handle(CreatePharmacyCmd request, CancellationToken cancellationToken)
    {
        var entity = await _dataLayer.HealthEssentialsContext.PharmacyEntities
            .FirstOrDefaultAsync(x => x.Guid == $"{request.EntityGuid}", CancellationToken.None);
        
        if (entity is null)
        {
            return new ()
            {
                Message = $"Pharmacy entity with Guid {request.Guid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        var pharmacy = request.Adapt<Domain.DataTransferObjects.XnelSystemsHealthEssentials.Pharmacy>();
        pharmacy.Guid = request.Guid is null ? $"{Guid.NewGuid()}" : $"{request.Guid}";
        pharmacy.Status = (int) GenericStatusType.Pending;
        pharmacy.Entity = entity;

        await _dataLayer.HealthEssentialsContext.Pharmacies.AddAsync(pharmacy, CancellationToken.None);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);

        request.Guid = Guid.Parse(pharmacy.Guid);
        return new()
        {
            Message = "Successfully created pharmacy",
            HttpStatusCode = HttpStatusCode.Accepted
        };
    }
}