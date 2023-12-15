using MediatR;
using XFramework.Domain.Generic.BusinessObjects;
using XFramework.Domain.Generic.Contracts.Requests;

namespace IdentityServer.Domain.Generic.Contracts.Requests;

public record ResetPasswordRequest : RequestBase, IRequest<CmdResponse>
{
    public string? PhoneEmailUsername { get; set; }
}