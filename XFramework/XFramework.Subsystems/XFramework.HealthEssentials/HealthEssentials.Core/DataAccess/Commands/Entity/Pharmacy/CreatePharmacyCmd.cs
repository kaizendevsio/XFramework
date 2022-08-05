using HealthEssentials.Domain.Generics.Contracts.Requests.Pharmacy.Create;

namespace HealthEssentials.Core.DataAccess.Commands.Entity.Pharmacy;

public class CreatePharmacyCmd : CreatePharmacyRequest, IRequest<CmdResponse<CreatePharmacyCmd>>
{ 
    
}