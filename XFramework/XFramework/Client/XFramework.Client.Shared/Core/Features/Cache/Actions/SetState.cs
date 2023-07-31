using IdentityServer.Domain.Generic.Contracts.Responses.Address;

namespace XFramework.Client.Shared.Core.Features.Cache;

public partial class CacheState
{
    public class SetState : BaseAction
    {
        public List<AddressCountryResponse> AddressEntityList { get; set; }
    }
}