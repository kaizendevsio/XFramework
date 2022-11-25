using HealthEssentials.Core.DataAccess.Query.Entity.Hospital;
using HealthEssentials.Domain.Generics.Contracts.Responses.Hospital;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Hospital;

public class GetHospitalLaboratoryHandler : QueryBaseHandler, IRequestHandler<GetHospitalLaboratoryQuery, QueryResponse<HospitalLaboratoryResponse>>
{
    public GetHospitalLaboratoryHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<HospitalLaboratoryResponse>> Handle(GetHospitalLaboratoryQuery request, CancellationToken cancellationToken)
    {
        var laboratory = await _dataLayer.HealthEssentialsContext.HospitalLaboratories
            .Include(x => x.Hospital)
            .Include(x => x.Laboratory)
            .AsSplitQuery()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        
        if (laboratory is null)
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
            Response = laboratory.Adapt<HospitalLaboratoryResponse>()
        };
    }
}