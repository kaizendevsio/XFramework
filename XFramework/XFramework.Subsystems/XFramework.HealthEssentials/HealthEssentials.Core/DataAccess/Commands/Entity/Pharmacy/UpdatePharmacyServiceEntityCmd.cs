using HealthEssentials.Domain.Generics.Contracts.Requests.Pharmacy.Update;

namespace HealthEssentials.Core.DataAccess.Commands.Entity.Pharmacy;

public class UpdatePharmacyServiceEntityCmd : UpdatePharmacyServiceEntityRequest, IRequest<CmdResponse<UpdatePharmacyServiceEntityCmd>>
{
    
}