using HealthEssentials.Core.DataAccess.Query.Entity.Patient;
using HealthEssentials.Domain.Generics.Contracts.Responses.Patient;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Patient;

public class GetPatientLaboratoryHandler : QueryBaseHandler, IRequestHandler<GetPatientLaboratoryQuery, QueryResponse<PatientLaboratoryResponse>>
{
    public GetPatientLaboratoryHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<PatientLaboratoryResponse>> Handle(GetPatientLaboratoryQuery request, CancellationToken cancellationToken)
    {
        var patientLaboratory = await _dataLayer.HealthEssentialsContext.PatientLaboratories
            .Include(x => x.LaboratoryJobOrder)
            .Include(x => x.Patient)
            .AsSplitQuery()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        
        if (patientLaboratory is null)
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
            Response = patientLaboratory.Adapt<PatientLaboratoryResponse>()
        };
    }
}