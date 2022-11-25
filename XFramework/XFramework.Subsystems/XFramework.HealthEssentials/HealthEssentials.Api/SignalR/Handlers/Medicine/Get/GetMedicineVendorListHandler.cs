using HealthEssentials.Core.DataAccess.Query.Entity.Medicine;
using HealthEssentials.Domain.Generics.Contracts.Requests.Medicine.Get;
using HealthEssentials.Domain.Generics.Contracts.Responses.Medicine;

namespace HealthEssentials.Api.SignalR.Handlers.Medicine.Get;

public class GetMedicineVendorListHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequestQuery<GetMedicineVendorListRequest, GetMedicineVendorListQuery, List<MedicineVendorResponse>>(connection, mediator);
    }
}