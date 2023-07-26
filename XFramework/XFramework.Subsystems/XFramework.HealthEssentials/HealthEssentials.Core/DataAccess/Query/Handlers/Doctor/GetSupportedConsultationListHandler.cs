using HealthEssentials.Core.DataAccess.Query.Entity.Doctor;
using HealthEssentials.Domain.Generics.Contracts.Responses.Doctor;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Doctor;

public class GetSupportedConsultationListHandler : QueryBaseHandler, IRequestHandler<GetSupportedConsultationListQuery, QueryResponse<List<DoctorConsultationResponse>>>
{
    public GetSupportedConsultationListHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<QueryResponse<List<DoctorConsultationResponse>>> Handle(GetSupportedConsultationListQuery request, CancellationToken cancellationToken)
    {
        var doctor = await _dataLayer.HealthEssentialsContext.Doctors
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Guid == $"{request.DoctorGuid}", cancellationToken: cancellationToken);
       
        if (doctor is null)
        {
            return new ()
            {
                Message = $"Doctor with Guid {request.DoctorGuid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        var consultations = await _dataLayer.HealthEssentialsContext.DoctorConsultations
            .Where(i => EF.Functions.ILike(i.Consultation.Name, $"%{request.SearchField}%"))
            .Where(i => i.DoctorId == doctor.Id)
            .Include(i => i.Consultation)
            .ThenInclude(i => i.Entity)
            .OrderBy(i => i.CreatedAt)
            .Take(request.PageSize)
            .AsSplitQuery()
            .AsNoTracking()
            .ToListAsync(CancellationToken.None);

        if (!consultations.Any())
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
            Message = "Records Found",
            
            Response = consultations.Adapt<List<DoctorConsultationResponse>>()
        };
    }
}