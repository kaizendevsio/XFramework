using HealthEssentials.Domain.Generics.Contracts.Requests.Medicine.Get;
using HealthEssentials.Domain.Generics.Contracts.Responses.Medicine;

namespace HealthEssentials.Core.DataAccess.Query.Entity.Medicine;

public class GetMedicineIntakeListQuery : GetMedicineIntakeListRequest, IRequest<QueryResponse<List<MedicineIntakeResponse>>>
{
    
}