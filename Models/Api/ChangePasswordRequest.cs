namespace CredentialProviderAPP.Models.Api
{
    public class ChangePasswordRequest
    {
        public string Login { get; set; } = string.Empty;
        public string NovaSenha { get; set; } = string.Empty;
    }
}