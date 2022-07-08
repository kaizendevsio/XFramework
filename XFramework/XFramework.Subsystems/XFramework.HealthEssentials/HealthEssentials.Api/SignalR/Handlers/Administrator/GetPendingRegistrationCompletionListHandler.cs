using HealthEssentials.Core.DataAccess.Query.Entity.Administrator;
using HealthEssentials.Core.DataAccess.Query.Entity.Consultation;
using HealthEssentials.Domain.Generics.Contracts.Requests.Administrtor;
using HealthEssentials.Domain.Generics.Contracts.Requests.Administrtor.Get;
using HealthEssentials.Domain.Generics.Contracts.Requests.Consultation;
using HealthEssentials.Domain.Generics.Contracts.Responses.Consultation;
using IdentityServer.Domain.Generic.Contracts.Responses;

namespace HealthEssentials.Api.SignalR.Handlers.Administrator;

public class GetPendingRegistrationCompletionListHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequestQuery<GetPendingRegistrationCompletionListRequest, GetPendingRegistrationCompletionListQuery, List<CredentialResponse>>(connection, mediator);
    }
}