using IdentityServer.Domain.Generic.Contracts.Responses;
using XFramework.Client.Shared.Entity.Models.Requests.Common;

namespace XFramework.Client.Shared.Core.Features.Session;

public partial class SessionState
{
    public class UpdateCredential : NavigableRequest, IRequest<CmdResponse>
    {
        public CredentialResponse Credential { get; set; }
    }
}