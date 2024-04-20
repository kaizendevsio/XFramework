using Address.Integration.Drivers;
using XFramework.Blazor.Entity.Enums;
using XFramework.Domain.Shared.Contracts;

namespace XFramework.Blazor.Core.Features.Address;

public partial class AddressState
{
    public record Fetch : StateAction
    {
        public AddressType AddressType { get; set; }
    }

    protected class FetchHandler(
        IAddressServiceWrapper addressServiceWrapper,
        HandlerServices handlerServices,
        IStore store)
        : StateActionHandler<Fetch>(handlerServices, store)
    {
        private AddressState CurrentState => Store.GetState<AddressState>();
        
        public override async Task Handle(Fetch action, CancellationToken aCancellationToken)
        {
            switch (action.AddressType)
            {
                case AddressType.NotSpecified:
                    break;
                case AddressType.Country:
                    var countryResponse = await addressServiceWrapper.AddressCountry.GetList(500, 1);
                    await HandleFailure(countryResponse, action, false);
                    CurrentState.CountryList = countryResponse.Response?.Items.ToList();
                    break;
                case AddressType.Region:
                    var regionResponse = await addressServiceWrapper.AddressRegion.GetList(
                        pageSize: 500, 
                        pageNumber: 1,
                        filter: [new ()
                            {
                                PropertyName = nameof(AddressRegion.CountryId),
                                Operation = QueryFilterOperation.Equal,
                                Value = CurrentState.SelectedCountry.Id
                            }
                        ]);
                    await HandleFailure(regionResponse, action, false);
                    CurrentState.RegionList = regionResponse.Response?.Items.ToList();
                    CurrentState.SelectedRegion = new();
                    CurrentState.SelectedCity = new();
                    CurrentState.SelectedBarangay = new();
                    break;
                case AddressType.Province:
                    var provinceResponse = await addressServiceWrapper.AddressProvince.GetList(
                        pageSize: 500, 
                        pageNumber: 1,
                        filter: [new ()
                            {
                                PropertyName = nameof(AddressProvince.RegCodeId),
                                Operation = QueryFilterOperation.Equal,
                                Value = CurrentState.SelectedRegion.Code
                            }
                        ]);
                    await HandleFailure(provinceResponse, action, false);
                    CurrentState.ProvinceList = provinceResponse.Response?.Items.ToList(); 
                    CurrentState.SelectedProvince = new(); 
                    CurrentState.SelectedCity = new();
                    CurrentState.SelectedBarangay = new();
                    break;
                case AddressType.City:
                    var cityResponse = await addressServiceWrapper.AddressCity.GetList(
                        pageSize: 500, 
                        pageNumber: 1,
                        filter: [new ()
                            {
                                PropertyName = nameof(AddressCity.ProvCodeId),
                                Operation = QueryFilterOperation.Equal,
                                Value = CurrentState.SelectedProvince.Code
                            }
                        ]);
                    await HandleFailure(cityResponse, action, false);
                    CurrentState.CityList = cityResponse.Response?.Items.ToList();
                    CurrentState.SelectedCity = new();
                    CurrentState.SelectedBarangay = new();
                    break;
                case AddressType.Barangay:
                    var barangayResponse = await addressServiceWrapper.AddressBarangay.GetList(
                        pageSize: 500, 
                        pageNumber: 1,
                        filter: [new ()
                            {
                                PropertyName = nameof(AddressBarangay.CityCodeId),
                                Operation = QueryFilterOperation.Equal,
                                Value = CurrentState.SelectedCity.Code
                            }
                        ]);
                    await HandleFailure(barangayResponse, action, false);
                    CurrentState.BarangayList = barangayResponse.Response?.Items.ToList();
                    CurrentState.SelectedBarangay = new();
                    break;
            }
            
            store.SetState(CurrentState);
            
        }
    }
}