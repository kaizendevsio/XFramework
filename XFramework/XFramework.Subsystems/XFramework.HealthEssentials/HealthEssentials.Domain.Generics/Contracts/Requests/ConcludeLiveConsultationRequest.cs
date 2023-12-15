using MediatR;
using XFramework.Domain.Generic.BusinessObjects;

namespace HealthEssentials.Domain.Generics.Contracts.Requests;

public record ConcludeLiveConsultationRequest(Guid Id) : RequestBase, IRequest<CmdResponse>;