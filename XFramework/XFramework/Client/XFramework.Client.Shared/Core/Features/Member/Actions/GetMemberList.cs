using XFramework.Domain.Generic.Contracts.Requests;

namespace XFramework.Client.Shared.Core.Features.Member;

public partial class MemberState
{
    public class GetMemberList : QueryableRequest, IRequest<CmdResponse>
    {
    }
}