using HealthEssentials.Core.DataAccess.Query.Entity.Patient;
using HealthEssentials.Domain.Generics.Contracts.Responses.Patient;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Patient;

public class GetPatientAilmentListHandler : QueryBaseHandler, IRequestHandler<GetPatientAilmentListQuery, QueryResponse<List<PatientAilmentResponse>>>
{
    public GetPatientAilmentListHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<List<PatientAilmentResponse>>> Handle(GetPatientAilmentListQuery request, CancellationToken cancellationToken)
    {
        var ailment = await _dataLayer.HealthEssentialsContext.PatientAilments
            .Include(x => x.Patient)
            .Include(x => x.Ailment)
            .OrderBy(x => x.CreatedAt)
            .Take(request.PageSize)
            .AsSplitQuery()
            .AsNoTracking()
            .ToListAsync(CancellationToken.None);
        
        if (!ailment.Any())
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
            Message = "Data found",
            IsSuccess = true,
            Response = ailment.Adapt<List<PatientAilmentResponse>>()
        };
    }
}