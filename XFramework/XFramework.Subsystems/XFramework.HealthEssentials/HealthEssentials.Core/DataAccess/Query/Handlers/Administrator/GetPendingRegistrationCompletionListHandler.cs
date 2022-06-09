using HealthEssentials.Core.DataAccess.Query.Entity.Administrator;
using HealthEssentials.Domain.Generics.Strings;
using IdentityServer.Domain.Generic.Contracts.Responses;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Administrator;

public class GetPendingRegistrationCompletionListHandler : QueryBaseHandler, IRequestHandler<GetPendingRegistrationCompletionListQuery, QueryResponse<List<CredentialResponse>>>
{
    public GetPendingRegistrationCompletionListHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<List<CredentialResponse>>> Handle(GetPendingRegistrationCompletionListQuery request,
        CancellationToken cancellationToken)
    {
        var application = await GetApplication(request.RequestServer.ApplicationId);

        var credentials = await _dataLayer.XnelSystemsContext.IdentityCredentials
            .Include(i => i.IdentityInfo)
            .Include(i => i.IdentityContacts)
            .ThenInclude(i => i.Entity)
            .Include(i => i.IdentityRoles)
            .ThenInclude(i => i.RoleEntity)
            .AsNoTracking()
            .AsSplitQuery()
            .Where(c => c.Application == application)
            .Where(i => i.IdentityRoles.Any(p =>
                p.RoleEntity.Guid == $"{IdentityRoleStrings.Doctor}" ||
                p.RoleEntity.Guid == $"{IdentityRoleStrings.Pharmacy}" ||
                p.RoleEntity.Guid == $"{IdentityRoleStrings.Logistics}" ||
                p.RoleEntity.Guid == $"{IdentityRoleStrings.Hospital}" ||
                p.RoleEntity.Guid == $"{IdentityRoleStrings.Laboratory}"))
            .ToListAsync(CancellationToken.None);

        if (credentials.Count == 0)
        {
            return new()
            {
                HttpStatusCode = HttpStatusCode.NoContent,
                Message = "No pending registration completion found",
                IsSuccess = false,
            };
        }

        var pendingRegistrationCompletion = (
            from credential in credentials
            let doctor = _dataLayer.HealthEssentialsContext.Doctors.Any(i => i.CredentialId == credential.Id)
            where !doctor
            let pharmacy = _dataLayer.HealthEssentialsContext.PharmacyMembers.Any(i => i.CredentialId == credential.Id)
            where !pharmacy
            let laboratory = _dataLayer.HealthEssentialsContext.LaboratoryMembers.Any(i => i.CredentialId == credential.Id)
            where !laboratory
            let logistic = _dataLayer.HealthEssentialsContext.LogisticRiders.Any(i => i.CredentialId == credential.Id)
            where !logistic
            select credential.Adapt<CredentialResponse>()).ToList();

        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Message = "Pending registration completion found",
            IsSuccess = true,
            Response = pendingRegistrationCompletion
        };
    }
}

