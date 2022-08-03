using HealthEssentials.Domain.Generics.Contracts.Requests.Medicine.Get;
using HealthEssentials.Domain.Generics.Contracts.Responses.Medicine;

namespace HealthEssentials.Core.DataAccess.Query.Entity.Medicine;

public class GetMedicineVendorListQuery : GetMedicineVendorListRequest, IRequest<QueryResponse<List<MedicineVendorResponse>>>
{
    
}