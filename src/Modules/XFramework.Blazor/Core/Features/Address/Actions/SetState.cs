using XFramework.Domain.Shared.Contracts;

namespace XFramework.Blazor.Core.Features.Address;

public partial class AddressState
{
    public record SetState : StateAction
    {
        public List<AddressCountry>? CountryList { get; set; }
        public List<AddressRegion>? RegionList { get; set; }
        public List<AddressProvince>? ProvinceList { get; set; }
        public List<AddressCity>? CityList { get; set; }
        public List<AddressBarangay>? BarangayList { get; set; }
    
        public AddressCountry? SelectedCountry { get; set; }
        public AddressRegion? SelectedRegion { get; set; }
        public AddressProvince? SelectedProvince { get; set; }
        public AddressCity? SelectedCity { get; set; }
        public AddressBarangay? SelectedBarangay { get; set; }
        public string? CurrentAddressName { get; set; }
        public string? CurrentUnitNumber { get; set; }
    }

    protected class SetStateHandler(HandlerServices handlerServices, IStore store)
        : StateActionHandler<SetState>(handlerServices, store)
    {
        private AddressState CurrentState => Store.GetState<AddressState>();

        public override async Task Handle(SetState action, CancellationToken aCancellationToken)
        {
            try
            {
                StateHelper.SetProperties(action, CurrentState);
                Persist(CurrentState);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            return;
        }
    }
}