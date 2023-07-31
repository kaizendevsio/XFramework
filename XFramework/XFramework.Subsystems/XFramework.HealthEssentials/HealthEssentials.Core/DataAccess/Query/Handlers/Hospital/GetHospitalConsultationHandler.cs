using HealthEssentials.Core.DataAccess.Query.Entity.Hospital;
using HealthEssentials.Domain.Generics.Contracts.Responses.Hospital;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Hospital;

public class GetHospitalConsultationHandler : QueryBaseHandler, IRequestHandler<GetHospitalConsultationQuery, QueryResponse<HospitalConsultationResponse>>
{
    public GetHospitalConsultationHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<HospitalConsultationResponse>> Handle(GetHospitalConsultationQuery request, CancellationToken cancellationToken)
    {
        var consultation = await _dataLayer.HealthEssentialsContext.HospitalConsultations
            .Include(x => x.Hospital)
            .Include(x => x.Consultation)
            .AsSplitQuery()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        
        if (consultation is null)
        {
            return new()
            {
                HttpStatusCode = HttpStatusCode.NoContent,
                Message = "No consultation found",
                
            };
        }

        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Message = "Record found",
            Response = consultation.Adapt<HospitalConsultationResponse>()
        };
    }
}