using XFramework.Client.Shared.Entity.Models.Requests.Common;

namespace XFramework.Client.Shared.Core.Features.Session;

public partial class SessionState
{
    public class InitiateVerificationCode : NavigableRequest, IRequest<CmdResponse>
    {
        public Guid? CredentialGuid { get; set; }
        public Action OnSuccess { get; set; }
        public Action OnFailure { get; set; }
        public GenericContactType ContactType { get; set; }
        public string Contact { get; set; }
        public bool? LocalVerification { get; set; }
        public string LocalToken { get; set; }

    }
}