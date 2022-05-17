using HealthEssentials.Core.DataAccess.Query.Entity.Pharmacy;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Pharmacy;

public class GetPharmacyListHandler : QueryBaseHandler, IRequestHandler<GetPharmacyListQuery, QueryResponse<List<PharmacyResponse>>>
{
    public GetPharmacyListHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<List<PharmacyResponse>>> Handle(GetPharmacyListQuery request, CancellationToken cancellationToken)
    {
        var pharmacy = await _dataLayer.HealthEssentialsContext.Pharmacies
            .AsNoTracking()
            .Include(i => i.PharmacyLocations)
            .Where(i => EF.Functions.ILike(i.Name, $"%{request.SearchField}%"))
            .Take(request.PageSize)
            .OrderBy(i => i.Name)
            .ToListAsync(CancellationToken.None);

        if (!pharmacy.Any())
        {
            return new()
            {
                HttpStatusCode = HttpStatusCode.NoContent,
                Message = "No Pharmacy Found",
                IsSuccess = true
            };
        }
        
        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Message = "Pharmacy Found",
            IsSuccess = true,
            Response = pharmacy.Adapt<List<PharmacyResponse>>()
        };
    }
}