using Microsoft.Extensions.Configuration;

namespace CredentialProviderAPP
{
    public static class ConfigHelper
    {
        private static IConfigurationRoot _config;

        static ConfigHelper()
        {
            _config = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false)
                .Build();
        }

        public static string Get(string key)
        {
            return _config[key];
        }
    }
}