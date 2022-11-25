using HealthEssentials.Core.DataAccess.Query.Entity.Medicine;
using HealthEssentials.Domain.Generics.Contracts.Requests.Medicine.Get;
using HealthEssentials.Domain.Generics.Contracts.Responses.Medicine;

namespace HealthEssentials.Api.SignalR.Handlers.Medicine.Get;

public class GetMedicineIntakeListHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequestQuery<GetMedicineIntakeListRequest, GetMedicineIntakeListQuery, List<MedicineIntakeResponse>>(connection, mediator);
    }
}