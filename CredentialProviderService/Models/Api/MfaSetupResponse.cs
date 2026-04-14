namespace CredentialProviderAPP.Models.Api
{
    public class MfaSetupResponse
    {
        public bool Sucesso { get; set; }
        public string Login { get; set; } = string.Empty;
        public string Nome { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? OtpAuthUrl { get; set; }
        public string? Erro { get; set; }
    }
}