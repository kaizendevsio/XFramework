namespace XFramework.Client.Shared.Core.Helpers;

public interface IHttpClient
{
    public Task<QueryResponse<TResponse>> GetJsonAsync<TResponse>(string url, bool useAuthentication = true, decimal version = 2.0m);
    public Task<QueryResponse<TResponse>> GetJsonAsync<TResponse>(string url, Dictionary<string,string> queryParams, bool useAuthentication = true, decimal version = 2.0m);
    public Task<QueryResponse<TResponse>> PostJsonAsync<TResponse, TRequest>(string url, TRequest request, Dictionary<string, string> queryParams = null, bool useAuthentication = false, decimal version = 2.0m);
    public Task<CmdResponse> PostJsonAsync<TRequest>(string url, TRequest request, bool useAuthentication = false, decimal version = 2.0m);
    public Task<CmdResponse> PostJsonAsync<TRequest>(string url, TRequest request, Dictionary<string, string> queryParams = null, bool useAuthentication = false, decimal version = 2.0m);
    public Task<CmdResponse> PatchJsonAsync<TRequest>(string url, TRequest request,Dictionary<string, string> queryParams = null, bool useAuthentication = false, decimal version = 2.0m);
    public Task<QueryResponse<TResponse>> PatchJsonAsync<TResponse, TRequest>(string url, TRequest request, bool useAuthentication = false, decimal version = 2.0m);
    public Task<QueryResponse<TResponse>> PatchJsonAsync<TResponse, TRequest>(string url, TRequest request, Dictionary<string,string> queryParams = null, bool useAuthentication = false, decimal version = 2.0m);
    public Task<QueryResponse<TResponse>> DeleteAsync<TResponse>(string url, Dictionary<string,string> queryParams, bool useAuthentication = true, decimal version = 2.0m);
    public Task<CmdResponse> DeleteAsync(string url, Dictionary<string,string> queryParams, bool useAuthentication = true, decimal version = 2.0m);
}