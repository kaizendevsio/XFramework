using HealthEssentials.Domain.Generics.Contracts.Requests.Pharmacy.Update;

namespace HealthEssentials.Core.DataAccess.Commands.Entity.Pharmacy;

public class UpdatePharmacyCmd : UpdatePharmacyRequest, IRequest<CmdResponse<UpdatePharmacyCmd>>
{
    
}