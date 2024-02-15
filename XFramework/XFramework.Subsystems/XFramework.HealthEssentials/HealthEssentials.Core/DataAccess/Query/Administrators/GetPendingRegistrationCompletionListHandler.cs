using HealthEssentials.Domain.Generics.Constants;
using XFramework.Domain.Generic.Contracts;

namespace HealthEssentials.Core.DataAccess.Query.Administrators;

public class GetPendingRegistrationCompletionListHandler(
    DbContext dbContext,
    DbContext dbContext2,
    DbContext dbContext3,
    DbContext dbContext4,
    IIdentityServerServiceWrapper identityServerService,
    ITenantService tenantService,
    ILogger<GetPendingRegistrationCompletionListHandler> logger
    ) 
    : IRequestHandler<GetPendingRegistrationCompletionListRequest, QueryResponse<List<IdentityCredential>>>
{

    public async Task<QueryResponse<List<IdentityCredential>>> Handle(GetPendingRegistrationCompletionListRequest request, CancellationToken cancellationToken)
    {
        var tenant = await tenantService.GetTenant(request.Metadata.TenantId);

        var response = await identityServerService.IdentityCredential.GetList(
            pageSize: 100_000,
            pageNumber: 0,
            tenantId: tenant.Id,
            includeNavigations: true,
            filter: new List<QueryFilter>()
            {
                
            }
            );
        
        if (response.HttpStatusCode is not HttpStatusCode.OK)
        {
            return new()
            {
                HttpStatusCode = response.HttpStatusCode,
                Message = response.Message
            };
        }
        
        var credentials = response.Response?.Items
            .Where(c => c.Tenant.Id == tenant.Id)
            .Where(i => i.IdentityRoles.Any(p =>
                p.Type.Id == HealthEssentialsIdentityRoles.Doctor ||
                p.Type.Id == HealthEssentialsIdentityRoles.Pharmacy ||
                p.Type.Id == HealthEssentialsIdentityRoles.Logistics ||
                p.Type.Id == HealthEssentialsIdentityRoles.Hospital ||
                p.Type.Id == HealthEssentialsIdentityRoles.Laboratory))
            .ToList();
           

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
            var doctor = dbContext.Set<Doctor>().AnyAsync(i => i.CredentialId == credential.Id, CancellationToken.None);
            var pharmacy = dbContext2.Set<PharmacyMember>().AnyAsync(i => i.CredentialId == credential.Id, CancellationToken.None);
            var laboratory = dbContext3.Set<LaboratoryMember>().AnyAsync(i => i.CredentialId == credential.Id, CancellationToken.None);
            var logistic = dbContext4.Set<LogisticRider>().AnyAsync(i => i.CredentialId == credential.Id, CancellationToken.None);
            
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

