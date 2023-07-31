using HealthEssentials.Core.DataAccess.Query.Entity.Administrator;
using HealthEssentials.Domain.DataTransferObjects;
using HealthEssentials.Domain.Generics.Strings;
using IdentityServer.Domain.Generic.Contracts.Responses;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Administrator;

public class GetPendingRegistrationCompletionListHandler : QueryBaseHandler, IRequestHandler<GetPendingRegistrationCompletionListQuery, QueryResponse<List<CredentialResponse>>>
{
    private readonly IDataLayer _dataLayer2;
    private readonly IDataLayer _dataLayer3;
    private readonly IDataLayer _dataLayer4;

    public GetPendingRegistrationCompletionListHandler(IDataLayer dataLayer, IDataLayer dataLayer2, IDataLayer dataLayer3, IDataLayer dataLayer4)
    {
        _dataLayer2 = dataLayer2;
        _dataLayer3 = dataLayer3;
        _dataLayer4 = dataLayer4;
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<List<CredentialResponse>>> Handle(GetPendingRegistrationCompletionListQuery request, CancellationToken cancellationToken)
    {
        var application = await GetApplication(request.RequestServer.ApplicationId);

        var credentials = await _dataLayer.XnelSystemsContext.IdentityCredentials
            .Include(i => i.IdentityInfo)
            .Include(i => i.IdentityContacts)
            .ThenInclude(i => i.Entity)
            .Include(i => i.IdentityRoles)
            .ThenInclude(i => i.RoleEntity)
            .Where(c => c.Application == application)
            .Where(i => i.IdentityRoles.Any(p =>
                p.RoleEntity.Guid == $"{IdentityRoleStrings.Doctor}" ||
                p.RoleEntity.Guid == $"{IdentityRoleStrings.Pharmacy}" ||
                p.RoleEntity.Guid == $"{IdentityRoleStrings.Logistics}" ||
                p.RoleEntity.Guid == $"{IdentityRoleStrings.Hospital}" ||
                p.RoleEntity.Guid == $"{IdentityRoleStrings.Laboratory}"))
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
            var doctor = _dataLayer.HealthEssentialsContext.Doctors.AnyAsync(i => i.CredentialGuid == credential.Guid, CancellationToken.None);
            var pharmacy = _dataLayer2.HealthEssentialsContext.PharmacyMembers.AnyAsync(i => i.CredentialGuid == credential.Guid, CancellationToken.None);
            var laboratory = _dataLayer3.HealthEssentialsContext.LaboratoryMembers.AnyAsync(i => i.CredentialGuid == credential.Guid, CancellationToken.None);
            var logistic = _dataLayer4.HealthEssentialsContext.LogisticRiders.AnyAsync(i => i.CredentialGuid == credential.Guid, CancellationToken.None);
            
            await Task.WhenAll( doctor, pharmacy, laboratory, logistic);
            
            if (!doctor.Result && !pharmacy.Result && !laboratory.Result && !logistic.Result) pendingRegistrationCompletion.Add(credential);
        }

        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Message = "Pending registration completion found",
            
            Response = pendingRegistrationCompletion.Adapt<List<CredentialResponse>>()
        };
    }
}

