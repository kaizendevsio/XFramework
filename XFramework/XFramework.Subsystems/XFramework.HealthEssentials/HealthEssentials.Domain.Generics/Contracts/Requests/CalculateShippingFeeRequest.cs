namespace HealthEssentials.Domain.Generics.Contracts.Requests;

using TRequest = CalculateShippingFeeRequest;
using TResponse = QueryResponse<ShippingCalculation>;

public record CalculateShippingFeeRequest : RequestBase,
    IRequest<TResponse>,
    IStreamflowRequest<TRequest, TResponse>
{
    public Guid SourceAddressId { get; set; }
    public Guid DestinationAddressId { get; set; }
};