using MediatR;
using XFramework.Domain.Generic.BusinessObjects;
using XFramework.Domain.Generic.Contracts.Requests;

namespace SmsGateway.Domain.Generic.Contracts.Requests.Update;

public record ConfirmSmsMessageSentRequest(Guid Id) : RequestBase, IRequest<CmdResponse>;