using HealthEssentials.Core.DataAccess.Query.Entity.Patient;
using HealthEssentials.Domain.Generics.Contracts.Responses.Patient;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Patient;

public class GetPatientAilmentDetailListHandler : QueryBaseHandler, IRequestHandler<GetPatientAilmentDetailListQuery, QueryResponse<List<PatientAilmentDetailResponse>>>
{
    public GetPatientAilmentDetailListHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<List<PatientAilmentDetailResponse>>> Handle(GetPatientAilmentDetailListQuery request, CancellationToken cancellationToken)
    {
        var detail = await _dataLayer.HealthEssentialsContext.PatientAilmentDetails
            .Include(x => x.ConsultationJobOrder)
            .Include(x => x.PatientAilment)
            .Where(x => EF.Functions.ILike(x.DoctorName, $"%{request.SearchField}%"))
            .OrderBy(x => x.DoctorName)
            .Take(request.PageSize)
            .AsSplitQuery()
            .AsNoTracking()
            .ToListAsync(CancellationToken.None);
        
        if (!detail.Any())
        {
            return new()
            {
                HttpStatusCode = HttpStatusCode.NoContent,
                Message = "No data found",
                
            };
        }
        
        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Message = "Data found",
            
            Response = detail.Adapt<List<PatientAilmentDetailResponse>>()
        };

    }
}