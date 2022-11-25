using HealthEssentials.Domain.Generics.Contracts.Requests.Medicine.Get;
using HealthEssentials.Domain.Generics.Contracts.Responses.Medicine;

namespace HealthEssentials.Core.DataAccess.Query.Entity.Medicine;

public class GetMedicineVendorQuery : GetMedicineVendorRequest, IRequest<QueryResponse<MedicineVendorResponse>>
{
    
}