using HealthEssentials.Domain.Generics.Contracts.Requests.Availability;

namespace HealthEssentials.Core.DataAccess.Commands.Entity.Availability;

public class DeleteAvailabilityCmd : DeleteAvailabilityRequest, IRequest<CmdResponse<DeleteAvailabilityCmd>>
{
    
}