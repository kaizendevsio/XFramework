using XFramework.Client.Shared.Entity.Models.Requests.Common;
using XFramework.Domain.Generic.Enums;

namespace XFramework.Client.Shared.Core.Features.Affiliate;

public partial class AffiliateState
{
    public class CreateSubscription : NavigableRequest, IRequest<CmdResponse>
    {
        public GenericContactType ContactType { get; set; }
    }
}
