using HealthEssentials.Domain.Generics.Contracts.Requests.Pharmacy.Delete;

namespace HealthEssentials.Core.DataAccess.Commands.Entity.Pharmacy;

public class DeletePharmacyJobOrderCmd : DeletePharmacyJobOrderRequest, IRequest<CmdResponse<DeletePharmacyJobOrderCmd>>
{
    
}