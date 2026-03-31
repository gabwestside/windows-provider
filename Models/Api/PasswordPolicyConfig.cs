namespace CredentialProviderAPP.Models.Api
{
    public class PasswordPolicyConfig
    {
        public int MinLength { get; set; }
        public int MinSpecialChars { get; set; }
        public string AllowedSpecialChars { get; set; } = string.Empty;
        public bool RequireUppercase { get; set; }
        public bool RequireLowercase { get; set; }
        public bool RequireNumber { get; set; }
    }
}