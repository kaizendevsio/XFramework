using HealthEssentials.Core.DataAccess.Commands.Entity.Logistic;
using HealthEssentials.Core.DataAccess.Commands.Entity.Pharmacy;
using HealthEssentials.Domain.DataTransferObjects;
using HealthEssentials.Domain.DataTransferObjects.XnelSystemsHealthEssentials;
using XFramework.Domain.Generic.Enums;
using XFramework.Integration.Services.Helpers;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Pharmacy;

public class CreatePharmacyMemberHandler : CommandBaseHandler, IRequestHandler<CreatePharmacyMemberCmd, CmdResponse<CreatePharmacyMemberCmd>>
{
    public CreatePharmacyMemberHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<CreatePharmacyMemberCmd>> Handle(CreatePharmacyMemberCmd request, CancellationToken cancellationToken)
    {
        var pharmacy = await _dataLayer.HealthEssentialsContext.Pharmacies.FirstOrDefaultAsync(i => i.Guid == $"{request.PharmacyGuid}", cancellationToken: cancellationToken);
        if (pharmacy is null)
        {
            return new ()
            {
                Message = $"Pharmacy with Guid {request.PharmacyGuid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        var credential = await _dataLayer.XnelSystemsContext.IdentityCredentials.FirstOrDefaultAsync(i => i.Guid == $"{request.CredentialGuid}", cancellationToken: cancellationToken);
        if (credential is null)
        {
            return new ()
            {
                Message = $"Credential with Guid {request.CredentialGuid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        var pharmacyLocation = await _dataLayer.HealthEssentialsContext.PharmacyLocations.FirstOrDefaultAsync(i => i.Guid == $"{request.PharmacyLocationGuid}", cancellationToken: cancellationToken);
        if (pharmacyLocation is null)
        {
            return new ()
            {
                Message = $"Pharmacy Location with Guid {request.Guid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }

        var pharmacyMember = request.Adapt<PharmacyMember>();
        pharmacyMember.Guid = request.Guid is null ? $"{Guid.NewGuid()}" : $"{request.Guid}";
        pharmacyMember.Pharmacy = pharmacy;
        pharmacyMember.CredentialGuid = credential.Guid;
        pharmacyMember.PharmacyLocation = pharmacyLocation;

        await _dataLayer.HealthEssentialsContext.PharmacyMembers.AddAsync(pharmacyMember, CancellationToken.None);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);

        request.Guid = Guid.Parse(pharmacyMember.Guid);
        return new()
        {
            Message = $"Pharmacy Member with Guid {pharmacyMember.Guid} created successfully",
            HttpStatusCode = HttpStatusCode.Accepted,
            
        };
    }
}