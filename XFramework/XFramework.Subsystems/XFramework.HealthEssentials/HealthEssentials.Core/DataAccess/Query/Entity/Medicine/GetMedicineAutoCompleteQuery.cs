using HealthEssentials.Domain.Generics.Contracts.Requests.Pharmacy.Get;
using HealthEssentials.Domain.Generics.Contracts.Responses.Medicine;

namespace HealthEssentials.Core.DataAccess.Query.Entity.Medicine;

public class GetMedicineAutoCompleteQuery : GetMedicineAutoCompleteRequest, IRequest<QueryResponse<List<MedicineResponse>>>
{
    
}