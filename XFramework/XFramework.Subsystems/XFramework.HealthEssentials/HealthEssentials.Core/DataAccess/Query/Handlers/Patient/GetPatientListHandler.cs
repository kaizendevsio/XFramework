using HealthEssentials.Core.DataAccess.Query.Entity.Patient;
using HealthEssentials.Domain.Generics.Contracts.Responses.Patient;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Patient;

public class GetPatientListHandler : QueryBaseHandler, IRequestHandler<GetPatientListQuery, QueryResponse<List<PatientResponse>>>
{
    public GetPatientListHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<List<PatientResponse>>> Handle(GetPatientListQuery request, CancellationToken cancellationToken)
    {
        var patient = await _dataLayer.HealthEssentialsContext.Patients
            .Include(i => i.Type)
            .ThenInclude(i => i.Group)
            .OrderBy(i => i.CreatedAt)
            .Take(request.PageSize)
            .AsSplitQuery()
            .AsNoTracking()
            .ToListAsync(CancellationToken.None);

        if (!patient.Any())
        {
            return new()
            {
                HttpStatusCode = HttpStatusCode.NoContent,
                Message = "No Patient Found",
                
            };
        }
        
        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Message = "Patient Found",
            
            Response = patient.Adapt<List<PatientResponse>>()
        };
    }
}