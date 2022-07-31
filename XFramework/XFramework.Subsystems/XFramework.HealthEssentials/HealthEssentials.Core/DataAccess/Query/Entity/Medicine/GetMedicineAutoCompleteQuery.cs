using HealthEssentials.Domain.Generics.Contracts.Requests.Pharmacy.Get;

namespace HealthEssentials.Core.DataAccess.Query.Entity.Medicine;

public class GetMedicineAutoCompleteQuery : GetMedicineAutoCompleteRequest, IRequest<QueryResponse<List<string>>>
{
    
}