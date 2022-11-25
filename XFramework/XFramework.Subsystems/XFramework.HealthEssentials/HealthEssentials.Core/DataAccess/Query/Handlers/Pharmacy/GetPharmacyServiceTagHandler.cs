using HealthEssentials.Core.DataAccess.Query.Entity.Pharmacy;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Pharmacy;

public class GetPharmacyServiceTagHandler : QueryBaseHandler, IRequestHandler<GetPharmacyServiceTagQuery, QueryResponse<PharmacyServiceTagResponse>>
{
    public GetPharmacyServiceTagHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<PharmacyServiceTagResponse>> Handle(GetPharmacyServiceTagQuery request, CancellationToken cancellationToken)
    {
        var serviceTag = await _dataLayer.HealthEssentialsContext.PharmacyServiceTags
            .Include(x => x.PharmacyService)
            .Include(x => x.Tag)
            .AsSplitQuery()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        
        if (serviceTag is null)
        {
            return new()
            {
                HttpStatusCode = HttpStatusCode.NoContent,
                Message = "No data found",
                IsSuccess = true
            };
        }

        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Message = "Data Found",
            Response = serviceTag.Adapt<PharmacyServiceTagResponse>()
        };
    }
}