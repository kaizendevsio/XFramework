using HealthEssentials.Domain.Generics.Contracts.Requests;
using Microsoft.Extensions.Logging;
using XFramework.Core.Services;
using XFramework.Domain.Contexts;
using XFramework.Domain.Generic.Contracts;

namespace HealthEssentials.Core.DataAccess.Query.Administrator;

public class GetPendingRegistrationCompletionListHandler(
    IMessageBusWrapper messageBusWrapper,
    IMessagingServiceWrapper messagingServiceWrapper,
    IHostEnvironment hostEnvironment,
    HealthEssentialsContext healthEssentialsContext,
    HealthEssentialsContext healthEssentialsContext2,
    HealthEssentialsContext healthEssentialsContext3,
    HealthEssentialsContext healthEssentialsContext4,
    AppDbContext appDbContext,
    ITenantService tenantService,
    ILogger<GetPendingRegistrationCompletionListHandler> logger
    ) 
    : IRequestHandler<GetPendingRegistrationCompletionListRequest, QueryResponse<List<IdentityCredential>>>
{

    public async Task<QueryResponse<List<IdentityCredential>>> Handle(GetPendingRegistrationCompletionListRequest request, CancellationToken cancellationToken)
    {
        var tenant = await tenantService.GetTenant(request.Metadata.TenantId);

        var credentials = await appDbContext.IdentityCredentials
            .Include(i => i.IdentityInfo)
            .Include(i => i.IdentityContacts)
            .ThenInclude(i => i.Type)
            .Include(i => i.IdentityRoles)
            .ThenInclude(i => i.Type)
            .Where(c => c.Tenant.Id == tenant.Id)
            .Where(i => i.IdentityRoles.Any(p =>
                p.Type.Id == IdentityRoleStrings.Doctor ||
                p.Type.Id == IdentityRoleStrings.Pharmacy ||
                p.Type.Id == IdentityRoleStrings.Logistics ||
                p.Type.Id == IdentityRoleStrings.Hospital ||
                p.Type.Id == IdentityRoleStrings.Laboratory))
            .AsNoTracking()
            .AsSplitQuery()
            .ToListAsync(CancellationToken.None);

        if (credentials.Count == 0)
        {
            return new()
            {
                HttpStatusCode = HttpStatusCode.NoContent,
                Message = "No pending registration completion found",
                
            };
        }

        var pendingRegistrationCompletion = new List<IdentityCredential>();
        foreach (var credential in credentials)
        {
            var doctor = healthEssentialsContext.Doctors.AnyAsync(i => i.CredentialId == credential.Id, CancellationToken.None);
            var pharmacy = healthEssentialsContext2.PharmacyMembers.AnyAsync(i => i.CredentialId == credential.Id, CancellationToken.None);
            var laboratory = healthEssentialsContext3.LaboratoryMembers.AnyAsync(i => i.CredentialId == credential.Id, CancellationToken.None);
            var logistic = healthEssentialsContext4.LogisticRiders.AnyAsync(i => i.CredentialId == credential.Id, CancellationToken.None);
            
            await Task.WhenAll( doctor, pharmacy, laboratory, logistic);
            
            if (!doctor.Result && !pharmacy.Result && !laboratory.Result && !logistic.Result) pendingRegistrationCompletion.Add(credential);
        }

        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Message = "Pending registration completion found",
            Response = pendingRegistrationCompletion.Adapt<List<IdentityCredential>>()
        };
    }
}

