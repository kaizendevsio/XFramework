using IdentityServer.Domain.Generic.Contracts.Responses;
using XFramework.Client.Shared.Entity.Models.Requests.Common;

namespace XFramework.Client.Shared.Core.Features.Session;

public partial class SessionState
{
    public class UpdateContact : NavigableRequest, IRequest<CmdResponse>
    {
        public ContactResponse Contact { get; set; }
    }
}