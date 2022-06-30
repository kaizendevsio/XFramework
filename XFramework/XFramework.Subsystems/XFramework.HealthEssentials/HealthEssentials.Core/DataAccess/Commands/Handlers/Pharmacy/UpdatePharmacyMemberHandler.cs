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
        var existingPharmacyMember = await _dataLayer.HealthEssentialsContext.PharmacyMembers.FirstOrDefaultAsync(i => i.Guid == $"{request.Guid}", cancellationToken: cancellationToken);
        if (existingPharmacyMember is null)
        {
            return new ()
            {
                Message = $"Pharmacy member with Guid {request.Guid} not found",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        var credential = await _dataLayer.XnelSystemsContext.IdentityCredentials.FirstOrDefaultAsync(i => i.Guid == $"{request.CredentialGuid}", cancellationToken: cancellationToken);
        if (credential is null)
        {
            return new ()
            {
                Message = $"Credential with Guid {request.CredentialGuid} not found",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        var pharmacy = await _dataLayer.HealthEssentialsContext.Pharmacies.FirstOrDefaultAsync(i => i.Guid == $"{request.PharmacyGuid}", cancellationToken: cancellationToken);
        if (pharmacy is null)
        {
            return new ()
            {
                Message = $"Pharmacy with Guid {request.PharmacyGuid} not found",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        var pharmacyLocation = await _dataLayer.HealthEssentialsContext.PharmacyLocations
            .FirstOrDefaultAsync(i => i.Guid == $"{request.Guid}", cancellationToken: cancellationToken);

        if (pharmacyLocation is null)
        {
            return new ()
            {
                Message = $"Pharmacy Location with Guid {request.Guid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        var updatedPharmacyMember = request.Adapt(existingPharmacyMember);
        updatedPharmacyMember.CredentialId = credential.Id;
        updatedPharmacyMember.Pharmacy = pharmacy;
        updatedPharmacyMember.PharmacyLocation = pharmacyLocation;
        
        _dataLayer.HealthEssentialsContext.Update(updatedPharmacyMember);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        return new()
        {
            Message = $"Pharmacy member with Guid {request.Guid} updated successfully",
            HttpStatusCode = HttpStatusCode.Accepted,
            IsSuccess = true,
            Request = new()
            {
                Guid = Guid.Parse(updatedPharmacyMember.Guid)
            }
        };
    }
}