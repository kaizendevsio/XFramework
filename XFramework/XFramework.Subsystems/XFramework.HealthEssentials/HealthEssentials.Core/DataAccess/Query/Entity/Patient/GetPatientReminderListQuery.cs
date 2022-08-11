using HealthEssentials.Domain.Generics.Contracts.Requests.Patient.Get;
using HealthEssentials.Domain.Generics.Contracts.Responses.Patient;

namespace HealthEssentials.Core.DataAccess.Query.Entity.Patient;

public class GetPatientReminderListQuery : GetPatientReminderListRequest, IRequest<QueryResponse<List<PatientReminderResponse>>>
{
    
}