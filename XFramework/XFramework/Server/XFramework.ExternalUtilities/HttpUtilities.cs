using XFramework.External.Models;
using Newtonsoft.Json;
using System.Net;
using System.Text;

namespace XFramework.External;

public class HttpUtilities
{
    public async Task<HttpResponseBO> PostAsync(Uri ApiUri, string url, object param, CookieCollection requestCookies = null, string contentType = "application/json")
    {
        CookieContainer cookies = new CookieContainer();
        HttpClientHandler handler = new HttpClientHandler();
        handler.CookieContainer = cookies;

        if (requestCookies != null)
        {
            int cookieCount = requestCookies.Count();
            for (int i = 0; i < cookieCount; i++)
            {
                handler.CookieContainer.Add(ApiUri, new Cookie(requestCookies.ElementAt(i).Name, requestCookies.ElementAt(i).Value));
            }
        }

        using (HttpClient _client = new HttpClient(handler) { BaseAddress = ApiUri, Timeout = TimeSpan.FromHours(2) })
        {
            //_client.DefaultRequestHeaders.Clear();
            HttpResponseMessage x = await _client.PostAsync(ApiUri.AbsoluteUri + "v2/" + url, new StringContent(JsonConvert.SerializeObject(param), Encoding.UTF8, "application/json"));
            CookieCollection responseCookies = cookies.GetCookies(ApiUri);

            if (x.IsSuccessStatusCode)
            {
                HttpResponseBO response = new HttpResponseBO();
                response.ResponseCookies = responseCookies;
                response.ResponseResult = await x.Content.ReadAsStringAsync();

                return response;
            }
            else
            {
                throw new System.ArgumentException(String.Format("{0}", x.ReasonPhrase));
            }

        }
    }

    public async Task<HttpResponseBO> GetAsync(Uri ApiUri, string url, object param = null, CookieCollection requestCookies = null, string contentType = "application/json")
    {
        CookieContainer cookies = new CookieContainer();
        HttpClientHandler handler = new HttpClientHandler();
        handler.CookieContainer = cookies;
        handler.ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => { return true; };

        if (requestCookies != null)
        {
            int cookieCount = requestCookies.Count();
            for (int i = 0; i < cookieCount; i++)
            {
                handler.CookieContainer.Add(ApiUri, new Cookie(requestCookies.ElementAt(i).Name, requestCookies.ElementAt(i).Value));
            }
        }

        using (HttpClient _client = new HttpClient(handler) { BaseAddress = ApiUri, Timeout = TimeSpan.FromHours(2) })
        {
            //_client.DefaultRequestHeaders.Clear();
            var requestModelJson = JsonConvert.SerializeObject(param);
            HttpResponseMessage x = await _client.GetAsync(ApiUri.AbsoluteUri + url + JsonToQuery(requestModelJson));
            CookieCollection responseCookies = cookies.GetCookies(ApiUri);

            if (x.IsSuccessStatusCode)
            {
                HttpResponseBO response = new HttpResponseBO();
                response.ResponseCookies = responseCookies;
                response.ResponseResult = await x.Content.ReadAsStringAsync();
                return response;
            }
            else
            {
                throw new System.ArgumentException(String.Format("{0}", x.ReasonPhrase));
            }



        }
    }

    public async Task<HttpResponseBO> PostAsyncXForm(Uri ApiUri, string url, object param, CookieCollection requestCookies = null, string contentType = "application/x-www-form-urlencoded")
    {
        CookieContainer cookies = new CookieContainer();
        HttpClientHandler handler = new HttpClientHandler();
        handler.CookieContainer = cookies;
        handler.ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => { return true; };

        if (requestCookies != null)
        {
            int cookieCount = requestCookies.Count();
            for (int i = 0; i < cookieCount; i++)
            {
                handler.CookieContainer.Add(ApiUri, new Cookie(requestCookies.ElementAt(i).Name, requestCookies.ElementAt(i).Value));
            }
        }

        using (HttpClient _client = new HttpClient(handler) { BaseAddress = ApiUri, Timeout = TimeSpan.FromHours(2) })
        {
            //_client.DefaultRequestHeaders.Clear();
            string _i = param.ToFormData().ReadAsStringAsync().Result;
            StringContent y = new StringContent(_i, Encoding.UTF8, contentType);
            HttpResponseMessage x = await _client.PostAsync(ApiUri.AbsoluteUri + url, y);
            CookieCollection responseCookies = cookies.GetCookies(ApiUri);

            if (x.IsSuccessStatusCode)
            {
                HttpResponseBO response = new HttpResponseBO();
                response.ResponseCookies = responseCookies;
                response.ResponseResult = await x.Content.ReadAsStringAsync();

                return response;
            }
            else
            {
                throw new System.ArgumentException(String.Format("{0}", x.ReasonPhrase));
            }

        }
    }

    public string JsonToQuery(string jsonQuery)
    {
        string str = "?";
        str += jsonQuery.Replace(":", "=").Replace("{", "").
            Replace("}", "").Replace(",", "&").
            Replace("\"", "");
        return str;
    }

        
}