namespace CredentialProviderAPP.Models.Api
{
    public class ValidatePasswordResponse
    {
        public bool Sucesso { get; set; }
        public bool Valida { get; set; }
        public string? Erro { get; set; }
    }
}