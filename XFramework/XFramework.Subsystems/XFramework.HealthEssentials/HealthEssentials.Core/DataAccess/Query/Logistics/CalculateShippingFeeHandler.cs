using System.Text.Json;
using ChatGPT.Net;
using Registry.Integration.Drivers;
using XFramework.Domain.Generic.Contracts;
using XFramework.Integration.Abstractions;

namespace HealthEssentials.Core.DataAccess.Query.Logistics;

public class CalculateShippingFeeHandler(
    DbContext dbContext,
    ITenantService tenantService,
    IHelperService helperService,
    ILogger<CalculateShippingFeeHandler> logger,
    IRegistryServiceWrapper registryService,
    IIdentityServerServiceWrapper identityServerService
)
    : IRequestHandler<CalculateShippingFeeRequest, QueryResponse<ShippingCalculation>>
{
    public async Task<QueryResponse<ShippingCalculation>> Handle(CalculateShippingFeeRequest request,
        CancellationToken cancellationToken)
    {
        var config = await registryService.RegistryConfigurationGroup.GetList(
            pageSize: 1,
            pageNumber: 1,
            filter:
            [
                new()
                {
                    PropertyName = nameof(RegistryConfigurationGroup.Name),
                    Operation = QueryFilterOperation.Equal,
                    Value = "OpenAi"
                }
            ],
            includeNavigations: true,
            includes:
            [
                $"{nameof(RegistryConfigurationGroup.RegistryConfigurations)}"
            ]
        );

        if (!config.IsSuccess)
        {
            throw new Exception("Unable to get OpenAI configuration");
        }
        
        var shippingFeeConfig = await registryService.RegistryConfigurationGroup.GetList(
            pageSize: 1,
            pageNumber: 1,
            filter:
            [
                new()
                {
                    PropertyName = nameof(RegistryConfigurationGroup.Name),
                    Operation = QueryFilterOperation.Equal,
                    Value = "Admin:Settings"
                }
            ],
            includeNavigations: true,
            includes:
            [
                $"{nameof(RegistryConfigurationGroup.RegistryConfigurations)}"
            ]
        );

        if (!config.IsSuccess)
        {
            throw new Exception("Unable to get OpenAI configuration");
        }

        // Get the source address
        var sourceAddress = await identityServerService.IdentityAddress.Get(
            id: request.SourceAddressId,
            includeNavigations: true,
            includes:
            [
                $"{nameof(IdentityAddress.AddressType)}",
                $"{nameof(IdentityAddress.Country)}",
                $"{nameof(IdentityAddress.Region)}",
                $"{nameof(IdentityAddress.Province)}",
                $"{nameof(IdentityAddress.City)}",
                $"{nameof(IdentityAddress.Barangay)}",
            ]
        );

        // Get the destination address
        var destinationAddress = await identityServerService.IdentityAddress.Get(
            id: request.DestinationAddressId,
            includeNavigations: true,
            includes:
            [
                $"{nameof(IdentityAddress.AddressType)}",
                $"{nameof(IdentityAddress.Country)}",
                $"{nameof(IdentityAddress.Region)}",
                $"{nameof(IdentityAddress.Province)}",
                $"{nameof(IdentityAddress.City)}",
                $"{nameof(IdentityAddress.Barangay)}",
            ]
        );
        
        if (!sourceAddress.IsSuccess || !destinationAddress.IsSuccess)
        {
            throw new Exception("Unable to get source or destination address");
        }
        
        var source = $"{sourceAddress.Response.Region.Description}, {sourceAddress.Response.Province.Description}, {sourceAddress.Response.City.Description}, {sourceAddress.Response.Barangay.Description ?? ""}";
        var destination = $"{destinationAddress.Response.Region.Description}, {destinationAddress.Response.Province.Description}, {destinationAddress.Response.City.Description}, {destinationAddress.Response.Barangay.Description ?? ""}";

        if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(destination))
        {
            throw new Exception("Source or destination address is empty");
        }
        
        var apiKey = config.Response.Items.FirstOrDefault()?.RegistryConfigurations
            .FirstOrDefault(i => i.Key == "ApiKey")?.Value;

        if (string.IsNullOrEmpty(apiKey))
        {
            throw new Exception("OpenAI ApiKey is not set");
        }

        var openai = new ChatGpt(apiKey);
        openai.Config.Model = "gpt-4-turbo-2024-04-09";
        
        var response = await openai.Ask($$"""
                                          Give an estimate how far a place is from source to destination, only respond in FOLLOWING JSON, DO NOT RESPOND ANYTHING ELSE!

                                          json format:
                                          {
                                            "source": "",
                                            "destination": "",
                                            "distance_km": 0.00,
                                            "accuracy": 0.00,
                                            "travel_time": "0-10 minutes",
                                            "note": ""
                                          }

                                          SOURCE: {{source.ToUpperInvariant()}}
                                          DESTINATION: {{destination.ToUpperInvariant()}}
                                          """);

        if (string.IsNullOrEmpty(response))
        {
            return new QueryResponse<ShippingCalculation>
            {
                Message = "Unable to calculate shipping fee"
            };
        }

        var shippingCalculation = JsonSerializer.Deserialize<ShippingCalculation>(response);
        var shippingSettings = decimal.Parse(shippingFeeConfig.Response.Items.FirstOrDefault()?.RegistryConfigurations
            .FirstOrDefault(i => i.Key == "Settings:ShippingFee")?.Value);

        shippingCalculation.ShippingFee = shippingCalculation.DistanceKm * shippingSettings;
        
        return new QueryResponse<ShippingCalculation>
        {
            HttpStatusCode = HttpStatusCode.OK,
            Response = shippingCalculation
        };
    }
}