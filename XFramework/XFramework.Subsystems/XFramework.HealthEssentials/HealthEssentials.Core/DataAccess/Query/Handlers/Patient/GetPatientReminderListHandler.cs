using HealthEssentials.Core.DataAccess.Query.Entity.Patient;
using HealthEssentials.Domain.Generics.Contracts.Responses.Patient;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Patient;

public class GetPatientReminderListHandler : QueryBaseHandler, IRequestHandler<GetPatientReminderListQuery, QueryResponse<List<PatientReminderResponse>>>
{
    public GetPatientReminderListHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<List<PatientReminderResponse>>> Handle(GetPatientReminderListQuery request, CancellationToken cancellationToken)
    {
        var reminder = await _dataLayer.HealthEssentialsContext.PatientReminders
            .Include(x => x.ConsultationJobOrder)
            .Include(x => x.Patient)
            .OrderBy(x => x.CreatedAt)
            .Take(request.PageSize)
            .AsSplitQuery()
            .AsNoTracking()
            .ToListAsync(CancellationToken.None);
          
        if (!reminder.Any())
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
            Response = reminder.Adapt<List<PatientReminderResponse>>()
        }; 

    }
}