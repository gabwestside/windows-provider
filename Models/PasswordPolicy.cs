namespace CredentialProviderAPP.Models
{
    public class PasswordPolicy
    {
        public int MinLength { get; set; }
        public int MinSpecialChars { get; set; }
        public string AllowedSpecialChars { get; set; } = "!@#$%&*";
        public bool RequireUppercase { get; set; }
        public bool RequireLowercase { get; set; }
        public bool RequireNumber { get; set; }
    }
}