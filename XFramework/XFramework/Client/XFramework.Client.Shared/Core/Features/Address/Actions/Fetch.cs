using Address.Integration.Drivers;
using IdentityServer.Integration.Drivers;
using XFramework.Client.Shared.Entity.Enums;

namespace XFramework.Client.Shared.Core.Features.Address;

public partial class AddressState
{
    public class Fetch : IRequest<CmdResponse>
    {
        public AddressType AddressType { get; set; }
    }

    protected class FetchHandler(
        IAddressServiceWrapper addressServiceWrapper,
        HandlerServices handlerServices,
        IStore store)
        : ActionHandler<Fetch, CmdResponse>(handlerServices, store)
    {
        private AddressState CurrentState => Store.GetState<AddressState>();
        
        public override async Task<CmdResponse> Handle(Fetch action, CancellationToken aCancellationToken)
        {
            switch (action.AddressType)
            {
                case AddressType.NotSpecified:
                    break;
                case AddressType.Country:
                    var countryResponse = await addressServiceWrapper.AddressCountry.GetList(500, 1);
                    await HandleFailure(countryResponse, action, false);
                    await Mediator.Send(new SetState() {CountryList = countryResponse.Response?.Items.ToList()});
                    break;
                case AddressType.Region:
                    var regionResponse = await addressServiceWrapper.AddressRegion.GetList(500, 1);
                    await HandleFailure(regionResponse, action, false);
                    await Mediator.Send(new SetState() {RegionList = regionResponse.Response?.Items.ToList(), SelectedRegion = new(), SelectedCity = new(), SelectedBarangay = new()});
                    break;
                case AddressType.Province:
                    var provinceResponse = await addressServiceWrapper.AddressProvince.GetList(500, 1);
                    await HandleFailure(provinceResponse, action, false);
                    await Mediator.Send(new SetState() {ProvinceList = provinceResponse.Response?.Items.ToList(), SelectedProvince = new(), SelectedCity = new(), SelectedBarangay = new()});
                    break;
                case AddressType.City:
                    var cityResponse = await addressServiceWrapper.AddressCity.GetList(500, 1);
                    await HandleFailure(cityResponse, action, false);
                    await Mediator.Send(new SetState() {CityList = cityResponse.Response?.Items.ToList(), SelectedCity = new(), SelectedBarangay = new()});
                    break;
                case AddressType.Barangay:
                    var barangayResponse = await addressServiceWrapper.AddressBarangay.GetList(500, 1);
                    await HandleFailure(barangayResponse, action, false);
                    await Mediator.Send(new SetState() {BarangayList = barangayResponse.Response?.Items.ToList(), SelectedBarangay = new()});
                    break;
            }

            return new()
            {
                HttpStatusCode = HttpStatusCode.Accepted,
            };
        }
    }
}