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
        var pharmacyMember = await _dataLayer.HealthEssentialsContext.PharmacyMembers.FirstOrDefaultAsync(i => i.Guid == $"{request.Guid}", cancellationToken: cancellationToken);
        if (pharmacyMember is null)
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
        
        pharmacyMember = request.Adapt(pharmacyMember);
        pharmacyMember.CredentialId = credential.Id;
        pharmacyMember.Pharmacy = pharmacy;
        
        _dataLayer.HealthEssentialsContext.Update(pharmacyMember);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        return new()
        {
            Message = "Laboratory updated successfully",
            HttpStatusCode = HttpStatusCode.Accepted,
            Request = request
        };
    }
}