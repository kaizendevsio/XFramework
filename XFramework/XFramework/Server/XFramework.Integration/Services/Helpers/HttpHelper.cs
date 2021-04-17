using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using XFramework.Domain.Generic.BusinessObjects;

namespace XFramework.Integration.Services.Helpers
{
    public class HttpHelper
    {
        public async Task<HttpResponseBO> PostAsync(Uri baseUrl, string url, object param, CookieCollection requestCookies = null, string contentType = "application/json", bool clearDefaultRequestHeaders = false)
        {
            var cookies = new CookieContainer();
            var handler = new HttpClientHandler {CookieContainer = cookies};
            using var client = new HttpClient(handler) {BaseAddress = baseUrl, Timeout = TimeSpan.FromHours(2)};
            
            if (clearDefaultRequestHeaders) client.DefaultRequestHeaders.Clear();
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

            if (!x.IsSuccessStatusCode) throw new ArgumentException($"{x.ReasonPhrase}");
            var response = new HttpResponseBO
            {
                ResponseCookies = responseCookies, 
                ResponseResult = await x.Content.ReadAsStringAsync()
            };
            return response;
        }
        public async Task<HttpResponseBO> GetAsync(Uri baseUrl, string url, object param = null, CookieCollection requestCookies = null, string contentType = "application/json", bool clearDefaultRequestHeaders = false)
        {
            var cookies = new CookieContainer();
            var handler = new HttpClientHandler
            {
                CookieContainer = cookies,
                ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true
            };
            using var client = new HttpClient(handler) {BaseAddress = baseUrl, Timeout = TimeSpan.FromHours(2)};
            if (clearDefaultRequestHeaders) client.DefaultRequestHeaders.Clear();
            if (requestCookies != null)
            {
                var cookieCount = requestCookies.Count;
                for (var i = 0; i < cookieCount; i++)
                {
                    handler.CookieContainer.Add(baseUrl, new Cookie(requestCookies.ElementAt(i).Name, requestCookies.ElementAt(i).Value));
                }
            }

            var requestModelJson = param != null ? JsonSerializer.Serialize(param).JsonToQuery() : string.Empty;
            HttpResponseMessage x = await client.GetAsync($"{baseUrl.AbsoluteUri}{url}{requestModelJson}");
            CookieCollection responseCookies = cookies.GetCookies(baseUrl);

            if (!x.IsSuccessStatusCode) throw new ArgumentException($"{x.ReasonPhrase}");
            
            var response = new HttpResponseBO
            {
                ResponseCookies = responseCookies,
                ResponseResult = await x.Content.ReadAsStringAsync()
            };
            return response;
        }
    }
}