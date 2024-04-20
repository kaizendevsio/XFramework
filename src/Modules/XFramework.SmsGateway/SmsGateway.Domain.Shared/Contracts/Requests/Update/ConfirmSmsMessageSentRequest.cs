using MediatR;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Contracts.Requests;

namespace SmsGateway.Domain.Shared.Contracts.Requests.Update;

public record ConfirmSmsMessageSentRequest(Guid Id) : RequestBase, IRequest<CmdResponse>;