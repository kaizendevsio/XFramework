using IdentityServer.Domain.Shared.Contracts.Responses;

namespace IdentityServer.Domain.Shared.Contracts.Requests;

[MemoryPackable]
public partial record HealthCheckRequest : RequestBase,
    IQuery<QueryResponse<HealthCheckResponse>>,
    IBoltRequest<HealthCheckRequest, QueryResponse<HealthCheckResponse>>
{
}
