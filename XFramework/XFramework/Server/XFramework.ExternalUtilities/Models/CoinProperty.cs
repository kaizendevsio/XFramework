namespace XFramework.External.Models;

using System;
using Newtonsoft.Json;

public partial class CoinProperty
{
    [JsonProperty("data")]
    public Data Data { get; set; }

    [JsonProperty("timestamp")]
    public long Timestamp { get; set; }
}

public partial class Data
{
    [JsonProperty("id")]
    public string Id { get; set; }

    [JsonProperty("rank")]
    [JsonConverter(typeof(ParseStringConverter))]
    public long Rank { get; set; }

    [JsonProperty("symbol")]
    public string Symbol { get; set; }

    [JsonProperty("name")]
    public string Name { get; set; }

    [JsonProperty("supply")]
    public string Supply { get; set; }

    [JsonProperty("maxSupply")]
    public string MaxSupply { get; set; }

    [JsonProperty("marketCapUsd")]
    public string MarketCapUsd { get; set; }

    [JsonProperty("volumeUsd24Hr")]
    public string VolumeUsd24Hr { get; set; }

    [JsonProperty("priceUsd")]
    public string PriceUsd { get; set; }

    [JsonProperty("changePercent24Hr")]
    public string ChangePercent24Hr { get; set; }

    [JsonProperty("vwap24Hr")]
    public string Vwap24Hr { get; set; }
}

public partial class CoinProperty
{
    public static CoinProperty FromJson(string json) => JsonConvert.DeserializeObject<CoinProperty>(json, XFramework.External.Models.Converter.Settings);
}

internal class ParseStringConverter : JsonConverter
{
    public override bool CanConvert(Type t) => t == typeof(long) || t == typeof(long?);

    public override object ReadJson(JsonReader reader, Type t, object existingValue, JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.Null) return null;
        var value = serializer.Deserialize<string>(reader);
        long l;
        if (Int64.TryParse(value, out l))
        {
            return l;
        }
        throw new Exception("Cannot unmarshal type long");
    }

    public override void WriteJson(JsonWriter writer, object untypedValue, JsonSerializer serializer)
    {
        if (untypedValue == null)
        {
            serializer.Serialize(writer, null);
            return;
        }
        var value = (long)untypedValue;
        serializer.Serialize(writer, value.ToString());
        return;
    }

    public static readonly ParseStringConverter Singleton = new ParseStringConverter();
}