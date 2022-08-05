using HealthEssentials.Domain.Generics.Contracts.Requests.Pharmacy.Update;

namespace HealthEssentials.Core.DataAccess.Commands.Entity.Pharmacy;

public class UpdatePharmacyLocationCmd : UpdatePharmacyLocationRequest, IRequest<CmdResponse<UpdatePharmacyLocationCmd>>
{
    
}