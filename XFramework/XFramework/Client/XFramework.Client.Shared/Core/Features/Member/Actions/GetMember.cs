using XFramework.Domain.Generic.Contracts.Requests;

namespace XFramework.Client.Shared.Core.Features.Member;

public partial class MemberState
{
    public class GetMember : QueryableRequest, IRequest<CmdResponse>
    {
        public Guid? CredentialGuid { get; set; }
    }
}