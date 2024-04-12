using System.Text.Json.Serialization;

namespace HealthEssentials.Domain.Generics.Contracts;

public class ShippingCalculation : BaseModel
{
    [JsonPropertyName("source")]
    public string? Source { get; set; }

    [JsonPropertyName("destination")]
    public string? Destination { get; set; }

    [JsonPropertyName("distance_km")]
    public decimal DistanceKm { get; set; }

    [JsonPropertyName("travel_time")]
    public string? TravelTime { get; set; }

    [JsonPropertyName("note")]
    public string? Note { get; set; }
    
    [JsonPropertyName("accuracy")]
    public decimal Accuracy { get; set; }

    public decimal ShippingFee { get; set; }
}