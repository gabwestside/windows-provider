using Microsoft.Extensions.Configuration;
using System.IO;

namespace CredentialProviderAPP.Config;

public static class AppConfig
{
    public static IConfiguration Configuration { get; }

    static AppConfig()
    {
        var basePath = AppDomain.CurrentDomain.BaseDirectory;

        Configuration = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: true)
            .Build();
    }

    public static string DatabasePath
    {
        get
        {
            var configPath = Configuration["Database:Path"];

            if (!string.IsNullOrWhiteSpace(configPath))
                return configPath;

            // fallback: banco ao lado do exe
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "mfa.db");
        }
    }
}