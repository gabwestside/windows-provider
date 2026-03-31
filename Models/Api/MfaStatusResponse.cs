namespace CredentialProviderAPP.Models.Api
{
    public class MfaStatusResponse
    {
        public bool Sucesso { get; set; }

        // "NotConfigured", "Pending", "Configured"
        public string Status { get; set; } = string.Empty;

        public string? Erro { get; set; }
    }
}