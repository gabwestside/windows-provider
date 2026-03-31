namespace CredentialProviderAPP.Models.Api
{
    public class ValidateMfaRequest
    {
        public string Login { get; set; } = string.Empty;
        public string Codigo { get; set; } = string.Empty;
    }
}