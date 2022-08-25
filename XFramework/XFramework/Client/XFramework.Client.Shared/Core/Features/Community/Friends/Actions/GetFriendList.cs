using XFramework.Domain.Generic.Contracts.Requests;

namespace XFramework.Client.Shared.Core.Features.Community;

public partial class CommunityState
{
    public class GetFriendList : QueryableRequest, IAction
    {
        public int Limit { get; set; }
        public Guid? CommunityIdentityGuid { get; set; }
    }
}