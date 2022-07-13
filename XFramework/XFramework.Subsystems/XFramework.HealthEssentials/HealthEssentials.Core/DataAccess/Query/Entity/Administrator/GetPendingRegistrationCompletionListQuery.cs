using HealthEssentials.Domain.Generics.Contracts.Requests.Administrtor;
using HealthEssentials.Domain.Generics.Contracts.Requests.Administrtor.Get;
using IdentityServer.Domain.Generic.Contracts.Responses;

namespace HealthEssentials.Core.DataAccess.Query.Entity.Administrator;

public class GetPendingRegistrationCompletionListQuery : GetPendingRegistrationCompletionListRequest, IRequest<QueryResponse<List<CredentialResponse>>>
{
    
}