using HealthEssentials.Domain.Generics.Contracts.Requests.Pharmacy.Delete;

namespace HealthEssentials.Core.DataAccess.Commands.Entity.Pharmacy;

public class DeletePharmacyServiceCmd : DeletePharmacyServiceRequest, IRequest<CmdResponse<DeletePharmacyServiceCmd>>
{
    
}