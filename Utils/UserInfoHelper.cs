using System.Text.Json;

namespace CredentialProviderAPP.Helpers
{
    public class UserInfo
    {
        public MfaInfo Mfa { get; set; } = new();
        public PasswordInfo Password { get; set; } = new();
    }

    public class MfaInfo
    {
        public string Status { get; set; } = "not-configured";
        public string Method { get; set; } = "";
    }

    public class PasswordInfo
    {
        public bool ForceReset { get; set; }
        public DateTime? LastChangeUtc { get; set; }
    }

    public static class UserInfoHelper
    {
        public static UserInfo Parse(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return new UserInfo();

            try
            {
                return JsonSerializer.Deserialize<UserInfo>(raw) ?? new UserInfo();
            }
            catch
            {
                return new UserInfo();
            }
        }

        public static string Build(UserInfo info)
        {
            return JsonSerializer.Serialize(info);
        }
    }
}