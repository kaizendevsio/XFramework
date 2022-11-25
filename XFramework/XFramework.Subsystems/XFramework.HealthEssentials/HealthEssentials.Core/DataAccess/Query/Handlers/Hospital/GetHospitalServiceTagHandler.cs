using HealthEssentials.Core.DataAccess.Query.Entity.Hospital;
using HealthEssentials.Domain.Generics.Contracts.Responses.Hospital;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Hospital;

public class GetHospitalServiceTagHandler : QueryBaseHandler, IRequestHandler<GetHospitalServiceTagQuery, QueryResponse<HospitalServiceTagResponse>>
{
    public GetHospitalServiceTagHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<HospitalServiceTagResponse>> Handle(GetHospitalServiceTagQuery request, CancellationToken cancellationToken)
    {
        var serviceTag = await _dataLayer.HealthEssentialsContext.HospitalServiceTags
            .Include(x => x.HospitalService)
            .Include(x => x.Tag)
            .AsSplitQuery()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        
        if (serviceTag is null)
        {
            return new()
            {
                HttpStatusCode = HttpStatusCode.NoContent,
                Message = "No record found",
                IsSuccess = true
            };
        }

        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Message = "Record found",
            Response = serviceTag.Adapt<HospitalServiceTagResponse>()
        };
    }
}