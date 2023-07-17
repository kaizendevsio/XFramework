using HealthEssentials.Core.DataAccess.Query.Entity.Patient;
using HealthEssentials.Domain.Generics.Contracts.Responses.Patient;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Patient;

public class GetPatientAilmentHandler : QueryBaseHandler, IRequestHandler<GetPatientAilmentQuery, QueryResponse<PatientAilmentResponse>>
{
    public GetPatientAilmentHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<PatientAilmentResponse>> Handle(GetPatientAilmentQuery request, CancellationToken cancellationToken)
    {
        var ailment = await _dataLayer.HealthEssentialsContext.PatientAilments
            .Include(x => x.Patient)
            .Include(x => x.Ailment)
            .AsSplitQuery()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        
        if (ailment is null)
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
            Response = ailment.Adapt<PatientAilmentResponse>()
        };
    }
}