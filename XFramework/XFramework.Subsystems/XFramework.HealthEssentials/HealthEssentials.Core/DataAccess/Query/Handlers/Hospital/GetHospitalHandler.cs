using HealthEssentials.Core.DataAccess.Query.Entity.Hospital;
using HealthEssentials.Domain.Generics.Contracts.Responses.Hospital;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Hospital;

public class GetHospitalHandler : QueryBaseHandler, IRequestHandler<GetHospitalQuery, QueryResponse<HospitalResponse>>
{
    public GetHospitalHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<HospitalResponse>> Handle(GetHospitalQuery request, CancellationToken cancellationToken)
    {
        var hospital = await _dataLayer.HealthEssentialsContext.Hospitals
            .Include(x => x.Type)
            .ThenInclude(x => x.Group)
            .AsSplitQuery()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        
        if (hospital is null)
        {
            return new()
            {
                HttpStatusCode = HttpStatusCode.NoContent,
                Message = "No record found",
                
            };
        }

        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Message = "Record found",
            Response = hospital.Adapt<HospitalResponse>()
        };
    }
}