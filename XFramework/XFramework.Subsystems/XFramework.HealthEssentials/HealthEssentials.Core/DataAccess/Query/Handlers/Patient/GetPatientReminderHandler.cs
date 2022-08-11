using HealthEssentials.Core.DataAccess.Query.Entity.Patient;
using HealthEssentials.Domain.Generics.Contracts.Responses.Patient;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Patient;

public class GetPatientReminderHandler : QueryBaseHandler, IRequestHandler<GetPatientReminderQuery, QueryResponse<PatientReminderResponse>>
{
    public GetPatientReminderHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<PatientReminderResponse>> Handle(GetPatientReminderQuery request, CancellationToken cancellationToken)
    {
        var reminder = await _dataLayer.HealthEssentialsContext.PatientReminders
            .Include(x => x.ConsultationJobOrder)
            .Include(x => x.Patient)
            .AsSplitQuery()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        
        if (reminder is null)
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
            Response = reminder.Adapt<PatientReminderResponse>()
        };
    }
}