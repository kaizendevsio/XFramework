using ConfigurationModelEnvironment = XFramework.Client.Shared.Entity.Models.Components.ConfigurationModelEnvironment;

namespace XFramework.Client.Shared.Core.Services
{
    public class ConfigurationService
    {

        public ConfigurationService(HttpClient httpClient, IWebAssemblyHostEnvironment webAssemblyHostEnvironment)
        {
            HttpClient = httpClient;
            WebAssemblyHostEnvironment = webAssemblyHostEnvironment;

            //GetConfiguration();
        }
        
        public HttpClient HttpClient { get; set; }
        public IWebAssemblyHostEnvironment WebAssemblyHostEnvironment { get; }
        public ConfigurationModel ConfigurationModel { get; set; }
        public ConfigurationModelEnvironment CurrentConfiguration
        {
            get => ConfigurationModel.Environment
                .FirstOrDefault(i => i.Name == WebAssemblyHostEnvironment.Environment);
        }

        public async Task GetConfiguration()
        {
            ConfigurationModel ??= await HttpClient.GetFromJsonAsync<ConfigurationModel>("configurations.json");
        }

        
    }
}