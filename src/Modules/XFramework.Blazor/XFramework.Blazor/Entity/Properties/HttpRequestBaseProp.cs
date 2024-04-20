namespace XFramework.Blazor.Entity.Properties
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