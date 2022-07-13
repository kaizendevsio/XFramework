using HealthEssentials.Domain.Generics.Contracts.Requests.Pharmacy.Update;

namespace HealthEssentials.Core.DataAccess.Commands.Entity.Pharmacy;

public class UpdatePharmacyMemberCmd : UpdatePharmacyMemberRequest, IRequest<CmdResponse<UpdatePharmacyMemberCmd>>
{
    
}