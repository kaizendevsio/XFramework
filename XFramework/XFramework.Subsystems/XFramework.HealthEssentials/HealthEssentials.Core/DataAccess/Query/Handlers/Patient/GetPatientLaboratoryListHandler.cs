using HealthEssentials.Core.DataAccess.Query.Entity.Patient;
using HealthEssentials.Domain.Generics.Contracts.Responses.Patient;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Patient;

public class GetPatientLaboratoryListHandler : QueryBaseHandler, IRequestHandler<GetPatientLaboratoryListQuery, QueryResponse<List<PatientLaboratoryResponse>>>
{
    public GetPatientLaboratoryListHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<List<PatientLaboratoryResponse>>> Handle(GetPatientLaboratoryListQuery request, CancellationToken cancellationToken)
    {
        var patientLaboratory = await _dataLayer.HealthEssentialsContext.PatientLaboratories
            .Include(x => x.Patient)
            .Include(x => x.LaboratoryJobOrder)
            .OrderBy(x => x.CreatedAt)
            .Take(request.PageSize)
            .AsSplitQuery()
            .AsNoTracking()
            .ToListAsync(CancellationToken.None);
        
        if (!patientLaboratory.Any())
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
            Response = patientLaboratory.Adapt<List<PatientLaboratoryResponse>>()
        }; 
    }
}