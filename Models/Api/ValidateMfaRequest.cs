namespace CredentialProviderAPP.Models.Api
{
    public class ValidateMfaRequest
    {
        public string? ClientMachine { get; set; }
        public string Login { get; set; } = "";
        public string Codigo { get; set; } = "";
        public string? Metodo { get; set; } // "app" ou "sms"
    }
}