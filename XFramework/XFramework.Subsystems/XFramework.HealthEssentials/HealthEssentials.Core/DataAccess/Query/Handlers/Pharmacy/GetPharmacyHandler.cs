using HealthEssentials.Core.DataAccess.Query.Entity.Pharmacy;
using HealthEssentials.Domain.Generics.Contracts.Responses.Storage;
using IdentityServer.Domain.Generic.Contracts.Responses;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Pharmacy;

public class GetPharmacyHandler : QueryBaseHandler, IRequestHandler<GetPharmacyQuery, QueryResponse<PharmacyResponse>>
{
    public GetPharmacyHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<PharmacyResponse>> Handle(GetPharmacyQuery request, CancellationToken cancellationToken)
    {
        var pharmacy = await _dataLayer.HealthEssentialsContext.Pharmacies
            .Include(i => i.Entity)
            .Include(i => i.PharmacyLocations)
            .Include(i => i.PharmacyMembers)
            .Where(i => i.Guid == $"{request.Guid}")
            .AsNoTracking()
            .AsSplitQuery()
            .FirstOrDefaultAsync(CancellationToken.None);

        if (pharmacy is null)
        {
            return new()
            {
                HttpStatusCode = HttpStatusCode.NoContent,
                Message = $"No pharmacy found with Guid: {request.Guid}",
                IsSuccess = true
            };
        }
        
        var response = pharmacy.Adapt<PharmacyResponse>();

        response.Files = _dataLayer.XnelSystemsContext.StorageFiles.AsNoTracking().Where(i => i.IdentifierGuid == response.Guid).ToList().Adapt<List<StorageFileResponse>>();
        for (var index = 0; index < response.PharmacyMembers.Count; index++)
        {
            response.PharmacyMembers[index].Credential = _dataLayer.XnelSystemsContext.IdentityCredentials
                .AsNoTracking()
                .Include(i => i.IdentityInfo)
                .Include(i => i.IdentityContacts)
                .ThenInclude(i => i.Entity)
                .AsSplitQuery()
                .Where(i => i.Id == response.PharmacyMembers[index].CredentialId)
                .FirstOrDefault()?
                .Adapt<CredentialResponse>();
        }

        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Message = "Pharmacy found",
            IsSuccess = true,
            Response = response
        };        
    }
}