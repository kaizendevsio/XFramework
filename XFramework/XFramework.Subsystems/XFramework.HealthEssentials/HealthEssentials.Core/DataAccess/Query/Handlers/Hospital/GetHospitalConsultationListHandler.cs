using HealthEssentials.Core.DataAccess.Query.Entity.Hospital;
using HealthEssentials.Domain.Generics.Contracts.Responses.Hospital;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Hospital;

public class GetHospitalConsultationListHandler : QueryBaseHandler, IRequestHandler<GetHospitalConsultationListQuery, QueryResponse<List<HospitalConsultationResponse>>>
{
    public GetHospitalConsultationListHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<List<HospitalConsultationResponse>>> Handle(GetHospitalConsultationListQuery request, CancellationToken cancellationToken)
    {
        var hospitalConsultation = await _dataLayer.HealthEssentialsContext.HospitalConsultations
            .Include(x => x.Consultation)
            .Include(x => x.Hospital)
            .OrderBy(x => x.CreatedAt)
            .Take(request.PageSize)
            .AsSplitQuery()
            .AsNoTracking()
            .ToListAsync(CancellationToken.None);
        
        if (!hospitalConsultation.Any())
        {
            return new()
            {
                HttpStatusCode = HttpStatusCode.NoContent,
                Message = "No records found",
                IsSuccess = true
            };
        }
        
        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Message = "Records found",
            IsSuccess = true,
            Response = hospitalConsultation.Adapt<List<HospitalConsultationResponse>>()
        };
    }
}