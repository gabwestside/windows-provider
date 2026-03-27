using Microsoft.Extensions.Configuration;
using System;
using System.IO;

namespace CredentialProviderAPP
{
    public static class ConfigHelper
    {
        private static readonly IConfigurationRoot _config;

        static ConfigHelper()
        {
            string configDirectory = @"C:\credentialprovider";
            string configFile = Path.Combine(configDirectory, "appsettings.json");

            if (!File.Exists(configFile))
            {
                throw new FileNotFoundException(
                    $"O arquivo de configuração não foi encontrado em '{configFile}'.");
            }

            _config = new ConfigurationBuilder()
                .SetBasePath(configDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();
        }

        public static string Get(string key)
        {
            string? value = _config[key];

            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidOperationException(
                    $"Configuração '{key}' não encontrada no appsettings.json.");
            }

            return value;
        }
    }
}