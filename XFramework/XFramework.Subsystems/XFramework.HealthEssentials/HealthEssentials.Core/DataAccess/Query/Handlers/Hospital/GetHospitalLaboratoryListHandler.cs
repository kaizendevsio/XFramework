using HealthEssentials.Core.DataAccess.Query.Entity.Hospital;
using HealthEssentials.Domain.Generics.Contracts.Responses.Hospital;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Hospital;

public class GetHospitalLaboratoryListHandler : QueryBaseHandler, IRequestHandler<GetHospitalLaboratoryListQuery, QueryResponse<List<HospitalLaboratoryResponse>>>
{
    public GetHospitalLaboratoryListHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<List<HospitalLaboratoryResponse>>> Handle(GetHospitalLaboratoryListQuery request, CancellationToken cancellationToken)
    {
        var laboratory = await _dataLayer.HealthEssentialsContext.HospitalLaboratories
            .Include(x => x.Hospital)
            .Include(x => x.Laboratory)
            .OrderBy(x => x.CreatedAt)
            .Take(request.PageSize)
            .AsSplitQuery()
            .AsNoTracking()
            .ToListAsync(CancellationToken.None);
            
        if (!laboratory.Any())
        {
            return new()
            {
                HttpStatusCode = HttpStatusCode.NoContent,
                Message = "No Hospital Laboratory Found",
                IsSuccess = true
            };
        }
        
        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Message = "Hospital Laboratory Found",
            IsSuccess = true,
            Response = laboratory.Adapt<List<HospitalLaboratoryResponse>>()
        };
        
    }
}