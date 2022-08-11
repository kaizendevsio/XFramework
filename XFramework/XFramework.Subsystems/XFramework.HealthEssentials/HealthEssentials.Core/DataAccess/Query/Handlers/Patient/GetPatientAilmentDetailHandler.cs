using HealthEssentials.Core.DataAccess.Query.Entity.Patient;
using HealthEssentials.Domain.Generics.Contracts.Responses.Patient;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Patient;

public class GetPatientAilmentDetailHandler : QueryBaseHandler, IRequestHandler<GetPatientAilmentDetailQuery, QueryResponse<PatientAilmentDetailResponse>>
{
    public GetPatientAilmentDetailHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<PatientAilmentDetailResponse>> Handle(GetPatientAilmentDetailQuery request, CancellationToken cancellationToken)
    {
        var detail = await _dataLayer.HealthEssentialsContext.PatientAilmentDetails
            .Include(x => x.ConsultationJobOrder)
            .Include(x => x.PatientAilment)
            .AsSplitQuery()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        
        if (detail is null)
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
            Response = detail.Adapt<PatientAilmentDetailResponse>()
        };
    }
}