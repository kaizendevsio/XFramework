using HealthEssentials.Core.DataAccess.Query.Entity.Patient;
using HealthEssentials.Domain.Generics.Contracts.Responses.Patient;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Patient;

public class GetPatientEntityHandler : QueryBaseHandler, IRequestHandler<GetPatientEntityQuery, QueryResponse<PatientEntityResponse>>
{
    public GetPatientEntityHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<PatientEntityResponse>> Handle(GetPatientEntityQuery request, CancellationToken cancellationToken)
    {
        var entity = await _dataLayer.HealthEssentialsContext.PatientEntities
            .Include(x => x.Group)
            .AsSplitQuery()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        
        if (entity is null)
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
            Response = entity.Adapt<PatientEntityResponse>()
        };
    }
}