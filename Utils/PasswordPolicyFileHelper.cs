using System;
using System.IO;
using System.Text.Json;
using CredentialProviderAPP.Models;

namespace CredentialProviderAPP.Utils
{
    public static class PasswordPolicyFileHelper
    {
        private static string PolicyPath
        {
            get
            {
                string path = ConfigHelper.Get("PasswordPolicy:PolicyPath");

                if (string.IsNullOrWhiteSpace(path))
                {
                    path = Path.Combine(
                        AppDomain.CurrentDomain.BaseDirectory,
                        "password_policy.json"
                    );
                }

                return path;
            }
        }

        public static bool Exists()
        {
            return File.Exists(PolicyPath);
        }

        public static PasswordPolicyConfig? Load()
        {
            if (!File.Exists(PolicyPath))
                return null;

            var json = File.ReadAllText(PolicyPath);
            return JsonSerializer.Deserialize<PasswordPolicyConfig>(json);
        }

        public static void Save(PasswordPolicyConfig policy)
        {
            string? directory = Path.GetDirectoryName(PolicyPath);

            if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            var json = JsonSerializer.Serialize(policy, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            File.WriteAllText(PolicyPath, json);
        }

        public static string GetPath()
        {
            return PolicyPath;
        }
    }
}