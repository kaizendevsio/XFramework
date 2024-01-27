using MediatR;
using XFramework.Domain.Generic.BusinessObjects;
using XFramework.Domain.Generic.Contracts;

namespace HealthEssentials.Domain.Generics.Contracts.Requests;

public record GetPendingRegistrationCompletionListRequest : QueryableRequest, IRequest<QueryResponse<List<IdentityCredential>>>;
