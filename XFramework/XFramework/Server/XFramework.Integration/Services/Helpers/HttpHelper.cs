using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using XFramework.Domain.Generic.Contracts.Requests;
using XFramework.Integration.Extensions;
using XFramework.Integration.Security;

namespace XFramework.Integration.Services.Helpers;

public class HttpHelper
{
    public IConfiguration Configuration { get; }
    public HttpHelper(IConfiguration configuration)
    {
        Configuration = configuration;
    }
    public async Task<HttpResponse<T>> PostAsync<T>(Uri baseUrl, string url, object param, CookieCollection requestCookies = null, string contentType = "application/json", bool clearDefaultRequestHeaders = false, AuthenticationHeaderValue authenticationHeader = null)
    {
        try
        {
            var cookies = new CookieContainer();
            var handler = new HttpClientHandler {CookieContainer = cookies};
            using var client = new HttpClient(handler) {BaseAddress = baseUrl, Timeout = TimeSpan.FromHours(2)};
            
            if (clearDefaultRequestHeaders) client.DefaultRequestHeaders.Clear();
            if (authenticationHeader != null)
            {
                client.DefaultRequestHeaders.Authorization = authenticationHeader;
            }
            if (requestCookies != null)
            {
                var cookieCount = requestCookies.Count;
                for (var i = 0; i < cookieCount; i++)
                {
                    handler.CookieContainer.Add(baseUrl, new Cookie(requestCookies.ElementAt(i).Name, requestCookies.ElementAt(i).Value));
                }
            }

            var serializeObject = JsonSerializer.Serialize(param);
            HttpResponseMessage x = await client.PostAsync($"{baseUrl.AbsoluteUri}{url}", new StringContent(serializeObject, Encoding.UTF8, contentType));
            CookieCollection responseCookies = cookies.GetCookies(baseUrl);
            var stringContent = x.Content.ReadAsStringAsync();
            
            var response = new HttpResponse<T>
            {
                StatusCode = x.StatusCode,
                IsSuccess = x.IsSuccessStatusCode,
                ReasonPhrase = x.ReasonPhrase,
                Cookies = responseCookies, 
                Result = typeof(T) != typeof(string) 
                    ? JsonSerializer.Deserialize<T>(x.Content.ReadAsStringAsync().Result, new JsonSerializerOptions(){PropertyNameCaseInsensitive = true} ) 
                    : (T)Activator.CreateInstance(typeof(T), x.Content.ReadAsStringAsync().Result.ToCharArray())
            };
            return response;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }
    public async Task<HttpResponse<T>> GetAsync<T>(Uri baseUrl, string url, object param = null, CookieCollection requestCookies = null, string contentType = "application/json", bool clearDefaultRequestHeaders = false, AuthenticationHeaderValue authenticationHeader = null)
    {
        try
        {
            var cookies = new CookieContainer();
            var handler = new HttpClientHandler
            {
                CookieContainer = cookies,
                ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true
            };
            using var client = new HttpClient(handler) {BaseAddress = baseUrl, Timeout = TimeSpan.FromHours(2)};
            if (clearDefaultRequestHeaders) client.DefaultRequestHeaders.Clear();
            if (authenticationHeader != null)
            {
                client.DefaultRequestHeaders.Authorization = authenticationHeader;
            }
            if (requestCookies != null)
            {
                var cookieCount = requestCookies.Count;
                for (var i = 0; i < cookieCount; i++)
                {
                    handler.CookieContainer.Add(baseUrl, new Cookie(requestCookies.ElementAt(i).Name, requestCookies.ElementAt(i).Value));
                }
            }

            HttpResponseMessage x;
            var requestModelJson = param != null ? JsonSerializer.Serialize(param).JsonToQuery() : string.Empty;
            try
            {
                x = await client.GetAsync($"{baseUrl.AbsoluteUri}{url}{requestModelJson}");
            }
            catch (Exception e)
            {
                return new HttpResponse<T>
                {
                    StatusCode = HttpStatusCode.BadRequest,
                    ReasonPhrase = $"{e.Message} : {e.InnerException?.Message}",
                    Result = (T)Activator.CreateInstance(typeof(T))
                };
            }
            CookieCollection responseCookies = cookies.GetCookies(baseUrl);
            var response = new HttpResponse<T>
            {
                StatusCode = x.StatusCode,
                ReasonPhrase = x.ReasonPhrase,
                Cookies = responseCookies, 
                IsSuccess = x.IsSuccessStatusCode,
                Result = typeof(T) != typeof(string) 
                    ? JsonSerializer.Deserialize<T>(x.Content.ReadAsStringAsync().Result, new JsonSerializerOptions(){PropertyNameCaseInsensitive = true}) 
                    : (T)Activator.CreateInstance(typeof(T), x.Content.ReadAsStringAsync().Result.ToCharArray())
            };
            
            return response;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
       
    }
    public async Task<HttpResponse<T>> SecurePostJsonAsync<T, TContent>(string requestUri, EncryptedRequestBase<TContent> requestContent,CookieCollection cookieCollection = null)
    {
        var encryptionSecret = Configuration.GetValue<string>("Application:PaddedEncryptionSecret");
        var keyString = $"{encryptionSecret}{DateTime.UtcNow:yyyyMMddHHmm}";

        requestContent.AspNetCoreSession = requestContent.AspNetCoreSession;
        requestContent.Content = requestContent.AsJsonContent().EncryptWithAes(keyString);
        requestContent.RsaSignature = $"{requestContent.AsJsonContent()}{keyString}".ToSha512();
            
        return await PostAsync<T>(new Uri(requestUri),"",requestContent, cookieCollection);
    }
}