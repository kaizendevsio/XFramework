using IdentityServer.Domain.Generic.Contracts.Responses;
using XFramework.Client.Shared.Interfaces;

namespace XFramework.Client.Shared.Core.Features.Session;

public partial class SessionState
{
    public class LoginEvent : IEvent
    {
        public HttpStatusCode StatusCode { get; set; }
        public AuthorizeIdentityResponse? Data { get; set; }
    }
}