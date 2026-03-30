using System;
using System.IO;

namespace CredentialProviderAPP.Config
{
    public static class AppConfig
    {
        public static string DatabasePath
        {
            get
            {
                var configPath = ConfigHelper.Get("Database:Path");

                if (!string.IsNullOrWhiteSpace(configPath))
                    return configPath;

                var baseDir = AppDomain.CurrentDomain.BaseDirectory;

                var db1 = Path.Combine(baseDir, "mfa.db");
                var db2 = Path.Combine(baseDir, "mfa");

                if (File.Exists(db1))
                    return db1;

                if (File.Exists(db2))
                    return db2;

                throw new Exception("Banco de dados MFA não encontrado.");
            }
        }

        public static string PasswordBlacklistPath
        {
            get
            {
                var configPath = ConfigHelper.Get("PasswordPolicy:BlacklistPath");

                if (!string.IsNullOrWhiteSpace(configPath))
                    return configPath;

                return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "palavrasproibidas.txt");
            }
        }
    }
}