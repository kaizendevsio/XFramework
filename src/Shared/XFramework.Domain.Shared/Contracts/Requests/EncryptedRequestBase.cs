using System.Text.Json;
using System.Text.Json.Serialization;

namespace XFramework.Domain.Shared.Contracts.Requests;

public class EncryptedRequestBase<T>
{
    public EncryptedRequestBase(T content)
    {
        ObjectContent = content;
    }
        
    [JsonPropertyName("aspNetCoreSession")]
    public string AspNetCoreSession { get; set; }
        
    [JsonPropertyName("rsaSignature")]
    public string RsaSignature { get; set; }
        
    public string Content { get; set; }
    private T ObjectContent { get; set; }

    public string AsJsonContent()
    {
        return JsonSerializer.Serialize(ObjectContent, new JsonSerializerOptions {ReferenceHandler = ReferenceHandler.IgnoreCycles});
    } 
}
    
public class EncryptedHttpRequest
{
    [JsonPropertyName("aspNetCoreSession")]
    public string AspNetCoreSession { get; set; }
        
    [JsonPropertyName("rsaSignature")]
    public string RsaSignature { get; set; }
        
    [JsonPropertyName("content")] 
    public string Content { get; set; }
}