using MediatR;
using XFramework.Domain.Generic.BusinessObjects;

namespace HealthEssentials.Domain.Generics.Contracts.Requests;

public record CommenceLiveConsultationRequest(Guid Id) : RequestBase, IRequest<CmdResponse>;