using System.Text.Json;

namespace XFramework.Client.Shared.Entity.Properties
{
    public class HttpRequestBaseProp<T> : BaseApiRequestProp
    {
        public HttpRequestBaseProp(T content)
        {
            Content = content;
        }

        public T Content { get; set; }

        public string ContentToJson()
        {
            return JsonSerializer.Serialize(Content);
        }
    }
}