namespace CredentialProviderAPP.Models.Api
{
    public class ValidateMfaResponse
    {
        public bool Sucesso { get; set; }
        public bool Valido { get; set; }
        public string? Erro { get; set; }
    }
}