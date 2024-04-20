namespace XFramework.Blazor.Entity.Properties
{
    public class BaseApiRequestProp
    {
        [JsonPropertyName("aspNetCoreSession")]
        public string AspNetCoreSession { get; set; }
        [JsonPropertyName("rsaSignature")] public string RsaSignature { get; set; }
    }
}