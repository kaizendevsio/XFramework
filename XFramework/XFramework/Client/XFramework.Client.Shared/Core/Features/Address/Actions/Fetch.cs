using XFramework.Client.Shared.Entity.Enums;

namespace XFramework.Client.Shared.Core.Features.Address;

public partial class AddressState
{
    public class Fetch : IRequest<CmdResponse>
    {
        public AddressType AddressType { get; set; }
    }
}