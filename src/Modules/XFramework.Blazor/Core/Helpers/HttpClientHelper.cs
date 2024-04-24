using IdentityServer.Domain.Shared.Contracts.Responses;

namespace XFramework.Blazor.Core.Helpers;

public class HttpClientHelper : IHttpClient
{
    public IStore Store { get; }
    private readonly HttpClient HttpClient;
    private SessionState SessionState => Store.GetState<SessionState>();
    private AuthenticateIdentityResponse Authentication { get; set; }
    public JsonSerializerOptions JsonSerializerOptions { get; set; }
    
    public HttpClientHelper(HttpClient httpClient, IStore store)
    {
        Store = store;
        HttpClient = httpClient;
        JsonSerializerOptions = new();
    }
        
    public async Task<QueryResponse<TResponse>> GetJsonAsync<TResponse>(string url, bool useAuthentication = true, decimal version = 2.0m)
    {
        var request = new HttpRequestMessage
        {
            Method = new("GET"),
            RequestUri = new(url),
        };
        request.Headers.Add("Accept", "*/*");
        if (useAuthentication)
        {
            request.Headers.Authorization = new("Bearer",Authentication.AccessToken);
        }

        var response = await HttpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine($"{response.StatusCode}; {response.ReasonPhrase}");
        }
            
        var responseModel = await response.Content.ReadFromJsonAsync<TResponse>();
        return new()
        {
            
            HttpStatusCode = response.StatusCode,
            Response = responseModel
        };
    }

    public async Task<QueryResponse<TResponse>> GetJsonAsync<TResponse>(string url, Dictionary<string, string> queryParams, bool useAuthentication = true, decimal version = 2.0m)
    {
        try
        {
            var stringBuilder = new StringBuilder();
            
            queryParams.TryAdd("api-version", version.ToString());
            foreach (var queryParam in queryParams)
            {
                stringBuilder.Append($"&{queryParam.Key}={queryParam.Value}");
            }
            var queryString = stringBuilder.ToString().Substring(1);
            
            var request = new HttpRequestMessage
            {
                Method = new("GET"),
                RequestUri = new($"{url}?{queryString}"),
            };
            
            request.Headers.Add("Accept", "*/*");
            if (useAuthentication)
            {
                request.Headers.Authorization = new("Bearer",Authentication.AccessToken);
            }

            var response = await HttpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"{response.StatusCode}; {response.ReasonPhrase}");
            }
            
            var responseModel = await response.Content.ReadFromJsonAsync<TResponse>();
            return new()
            {
                
                HttpStatusCode = response.StatusCode,
                Response = responseModel
            };
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }

    public async Task<QueryResponse<TResponse>> PostJsonAsync<TResponse, TRequest>(string url ,TRequest request,  Dictionary<string, string> queryParams = null, bool useAuthentication = false, decimal version = 2.0m)
    {
        try
        {
            queryParams ??= new();
            var stringBuilder = new StringBuilder();
            
            queryParams.TryAdd("api-version", version.ToString());
            foreach (var queryParam in queryParams)
            {
                stringBuilder.Append($"&{queryParam.Key}={queryParam.Value}");
            }
            var queryString = stringBuilder.ToString().Substring(1);
            
            var httpRequest = new HttpRequestMessage
            {
                Method = new("POST"),
                RequestUri = new($"{url}?{queryString}"),
                Content = JsonContent.Create(request, null, JsonSerializerOptions)
            };
        
            httpRequest.Headers.Add("Accept", "*/*");
            if (useAuthentication)
            {
                httpRequest.Headers.Authorization = new("Bearer",Authentication.AccessToken);
            }
            
            var response = await HttpClient.SendAsync(httpRequest);
            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"{response.ReasonPhrase}; {await response.Content.ReadAsStringAsync()}");
                return new()
                {
                    
                    HttpStatusCode = response.StatusCode,
                    Response = Activator.CreateInstance<TResponse>()
                };
            }

            if (string.IsNullOrEmpty(await response.Content.ReadAsStringAsync())) return Activator.CreateInstance<QueryResponse<TResponse>>();
            
            var responseModel = await response.Content.ReadFromJsonAsync<TResponse>();
            return new()
            {
                
                HttpStatusCode = response.StatusCode,
                Response = responseModel
            };
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }

    public async Task<CmdResponse> PostJsonAsync<TRequest>(string url, TRequest request, bool useAuthentication = false, decimal version = 2.0m)
    {
        try
        {
            var httpRequest = new HttpRequestMessage
            {
                Method = new("POST"),
                RequestUri = new($"{url}?api-version={version}"),
                Content = JsonContent.Create(request, null, JsonSerializerOptions)
            };
            
            httpRequest.Headers.Add("Accept", "*/*");
            if (useAuthentication)
            {
                httpRequest.Headers.Authorization = new("Bearer",Authentication.AccessToken);
            }
            
            var response = await HttpClient.SendAsync(httpRequest);
            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"{response.ReasonPhrase}; {await response.Content.ReadAsStringAsync()}");
                return new()
                {
                    
                    HttpStatusCode = response.StatusCode,
                };
            }

            if (string.IsNullOrEmpty(await response.Content.ReadAsStringAsync())) return Activator.CreateInstance<CmdResponse>();
            return new ()
            {
                
                HttpStatusCode = response.StatusCode,
            };
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }

    public async Task<CmdResponse> PostJsonAsync<TRequest>(string url, TRequest request, Dictionary<string, string> queryParams = null, bool useAuthentication = false, decimal version = 2.0m)
    {
        try
        {
            queryParams ??= new();
            var stringBuilder = new StringBuilder();
            
            queryParams.TryAdd("api-version", version.ToString());
            foreach (var queryParam in queryParams)
            {
                stringBuilder.Append($"&{queryParam.Key}={queryParam.Value}");
            }
            var queryString = stringBuilder.ToString().Substring(1);
            
            var httpRequest = new HttpRequestMessage
            {
                Method = new("POST"),
                RequestUri = new($"{url}?{queryString}"),
                Content = JsonContent.Create(request, null, JsonSerializerOptions)
            };
        
            httpRequest.Headers.Add("Accept", "*/*");
            if (useAuthentication)
            {
                httpRequest.Headers.Authorization = new("Bearer",Authentication.AccessToken);
            }
            
            var response = await HttpClient.SendAsync(httpRequest);
            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"{response.ReasonPhrase}; {await response.Content.ReadAsStringAsync()}");
                return new ()
                {
                    
                    HttpStatusCode = response.StatusCode
                };
            }

            if (string.IsNullOrEmpty(await response.Content.ReadAsStringAsync())) return new();
            
            return new ()
            {
                
                HttpStatusCode = response.StatusCode
            };
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }

    public async Task<CmdResponse> PatchJsonAsync<TRequest>(string url, TRequest request, Dictionary<string, string> queryParams = null, bool useAuthentication = false, decimal version = 2.0m)
    {
        try
        {
            var stringBuilder = new StringBuilder();
            queryParams ??= new();
            queryParams.TryAdd("api-version", version.ToString());
            foreach (var queryParam in queryParams)
            {
                stringBuilder.Append($"&{queryParam.Key}={queryParam.Value}");
            }

            var queryString = stringBuilder.ToString().Substring(1);

            var httpRequest = new HttpRequestMessage
            {
                Method = new("PATCH"),
                RequestUri = new($"{url}?{queryString}"),
                Content = JsonContent.Create(request, null, JsonSerializerOptions)
            };

            httpRequest.Headers.Add("Accept", "*/*");
            if (useAuthentication)
            {
                httpRequest.Headers.Authorization = new("Bearer", Authentication.AccessToken);
            }


            var response = await HttpClient.SendAsync(httpRequest);
            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"{response.StatusCode}; {response.ReasonPhrase}");
            }

            if (string.IsNullOrEmpty(await response.Content.ReadAsStringAsync()))
                return Activator.CreateInstance<CmdResponse>();

            return new()
            {
                
                HttpStatusCode = response.StatusCode,
            };
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return new()
            {
                
                HttpStatusCode = HttpStatusCode.InternalServerError,
            };
        }
    }

    public async Task<QueryResponse<TResponse>> PatchJsonAsync<TResponse, TRequest>(string url, TRequest request, bool useAuthentication = false, decimal version = 2.0m)
    {
        try
        {
            var stringBuilder = new StringBuilder();
            var httpRequest = new HttpRequestMessage
            {
                Method = new("PATCH"),
                RequestUri = new($"{url}?api-version={version}"),
                Content = JsonContent.Create(request, null, JsonSerializerOptions)
            };
            
            httpRequest.Headers.Add("Accept", "*/*");
            if (useAuthentication)
            {
                httpRequest.Headers.Authorization = new("Bearer",Authentication.AccessToken);
            }

            var response = await HttpClient.SendAsync(httpRequest);
            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"{response.StatusCode}; {response.ReasonPhrase}");
            }
            
            if (string.IsNullOrEmpty(await response.Content.ReadAsStringAsync())) return Activator.CreateInstance<QueryResponse<TResponse>>();
            
            var responseModel = await response.Content.ReadFromJsonAsync<TResponse>();
            return new()
            {
                
                HttpStatusCode = response.StatusCode,
                Response = responseModel
            };
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }

    public async Task<QueryResponse<TResponse>> PatchJsonAsync<TResponse, TRequest>(string url, TRequest request, Dictionary<string, string> queryParams = null, bool useAuthentication = false, decimal version = 2.0m)
    {
        try
        {
            var stringBuilder = new StringBuilder();
            queryParams ??= new();
            queryParams.TryAdd("api-version", version.ToString());
            foreach (var queryParam in queryParams)
            {
                stringBuilder.Append($"&{queryParam.Key}={queryParam.Value}");
            }
            var queryString = stringBuilder.ToString().Substring(1);
            
            var httpRequest = new HttpRequestMessage
            {
                Method = new("PATCH"),
                RequestUri = new($"{url}?{queryString}"),
                Content = JsonContent.Create(request, null, JsonSerializerOptions)
            };
            
            httpRequest.Headers.Add("Accept", "*/*");
            if (useAuthentication)
            {
                httpRequest.Headers.Authorization = new("Bearer",Authentication.AccessToken);
            }

            var response = await HttpClient.SendAsync(httpRequest);
            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"{response.StatusCode}; {response.ReasonPhrase}");
            }

            if (string.IsNullOrEmpty(await response.Content.ReadAsStringAsync())) return Activator.CreateInstance<QueryResponse<TResponse>>();
            
            var responseModel = await response.Content.ReadFromJsonAsync<TResponse>();
            return new()
            {
                
                HttpStatusCode = response.StatusCode,
                Response = responseModel
            };

        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }

    public async Task<QueryResponse<TResponse>> DeleteAsync<TResponse>(string url, Dictionary<string, string> queryParams, bool useAuthentication = true, decimal version = 2.0m)
    {
        throw new NotImplementedException();
    }

    public async Task<CmdResponse> DeleteAsync(string url, Dictionary<string, string> queryParams, bool useAuthentication = true, decimal version = 2.0m)
    {
        try
        {
            var stringBuilder = new StringBuilder();
            
            queryParams.TryAdd("api-version", version.ToString());
            foreach (var queryParam in queryParams)
            {
                stringBuilder.Append($"&{queryParam.Key}={queryParam.Value}");
            }
            var queryString = stringBuilder.ToString().Substring(1);
            var request = new HttpRequestMessage
            {
                Method = new("DELETE"),
                RequestUri = new($"{url}?{queryString}"),
            };
        
            request.Headers.Add("Accept", "*/*");
            if (useAuthentication)
            {
                request.Headers.Authorization = new("Bearer",Authentication.AccessToken);
            }

            var response = await HttpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"{response.StatusCode}; {response.ReasonPhrase}");
            }
            
            var responseModel = await response.Content.ReadAsStringAsync();
            return new()
            {
                
                HttpStatusCode = response.StatusCode,
                Message = responseModel
            };
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }
}