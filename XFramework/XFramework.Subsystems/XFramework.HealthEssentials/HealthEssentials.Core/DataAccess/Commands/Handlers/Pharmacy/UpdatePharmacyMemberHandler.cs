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
        var updatedPharmacyMember = request.Adapt(existingPharmacyMember);

        if (request.CredentialGuid is not null)
        {
            var credential = await _dataLayer.XnelSystemsContext.IdentityCredentials.FirstOrDefaultAsync(i => i.Guid == $"{request.CredentialGuid}", CancellationToken.None);
            if (credential is null)
            {
                return new ()
                {
                    Message = $"Credential with Guid {request.CredentialGuid} does not exist",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }
            updatedPharmacyMember.CredentialId = credential.Id;
        }

        if (request.PharmacyGuid is not null)
        {
            var pharmacy = await _dataLayer.HealthEssentialsContext.Pharmacies.FirstOrDefaultAsync(i => i.Guid == $"{request.PharmacyGuid}", cancellationToken: cancellationToken);
            if (pharmacy is null)
            {
                return new ()
                {
                    Message = $"Pharmacy with Guid {request.PharmacyGuid} not found",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }
            updatedPharmacyMember.Pharmacy = pharmacy;
        }

        if (request.PharmacyLocationGuid is not null)
        {
            var pharmacyLocation = await _dataLayer.HealthEssentialsContext.PharmacyLocations.FirstOrDefaultAsync(i => i.Guid == $"{request.Guid}", cancellationToken: cancellationToken);
            if (pharmacyLocation is null)
            {
                return new ()
                {
                    Message = $"Pharmacy Location with Guid {request.Guid} does not exist",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }
            updatedPharmacyMember.PharmacyLocation = pharmacyLocation;
        }

        _dataLayer.HealthEssentialsContext.Update(updatedPharmacyMember);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        return new()
        {
            Message = $"Pharmacy member updated successfully",
            HttpStatusCode = HttpStatusCode.Accepted,
            IsSuccess = true,
            Request = new()
            {
                Guid = Guid.Parse(updatedPharmacyMember.Guid)
            }
        };
    }
}