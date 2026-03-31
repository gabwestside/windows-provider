namespace CredentialProviderAPP.Models.Api
{

public class MfaStatusResponse
{
    public bool Sucesso { get; set; }
    public string Status { get; set; } = "";
    public string? Metodo { get; set; } // "app" ou "sms"
    public string? Erro { get; set; }
}
}