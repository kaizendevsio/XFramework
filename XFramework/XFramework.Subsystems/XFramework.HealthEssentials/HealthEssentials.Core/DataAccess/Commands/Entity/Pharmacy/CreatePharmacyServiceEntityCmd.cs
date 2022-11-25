using HealthEssentials.Domain.Generics.Contracts.Requests.Pharmacy.Create;

namespace HealthEssentials.Core.DataAccess.Commands.Entity.Pharmacy;

public class CreatePharmacyServiceEntityCmd : CreatePharmacyServiceEntityRequest, IRequest<CmdResponse<CreatePharmacyServiceEntityCmd>>
{
    
}