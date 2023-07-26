using System.Text.Json;
using System.Text.Json.Serialization;

namespace XFramework.Domain.Generic.Contracts.Requests;

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
        return JsonSerializer.Serialize(ObjectContent);
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