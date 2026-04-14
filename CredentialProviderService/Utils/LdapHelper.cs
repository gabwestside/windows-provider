namespace CredentialProviderAPP.Utils
{
    public static class LdapHelper
    {
        public static string Escape(string input)
        {
            if (string.IsNullOrEmpty(input)) return "";

            return input
                .Replace("\\", "\\5c")
                .Replace("*", "\\2a")
                .Replace("(", "\\28")
                .Replace(")", "\\29");
        }

        public static string NormalizeLogin(string login)
        {
            if (string.IsNullOrWhiteSpace(login)) return "";

            if (login.Contains("\\"))
                login = login.Split('\\')[1];

            if (login.Contains("@"))
                login = login.Split('@')[0];

            return login.Trim();
        }
    }
}