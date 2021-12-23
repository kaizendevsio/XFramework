namespace XFramework.Client.Shared.Entity.Properties
{
    public class Configuration
    {
        public List<ConfigurationModelEnvironment> Environment { get; set; }   
    }

    public class ConfigurationModelEnvironment
    {
        public string Name { get; set; }
        public string ApiBaseUri { get; set; }
        public string StreamBaseUri { get; set; }
        public long ApplicationId { get; set; }
    }
}