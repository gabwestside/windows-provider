namespace CredentialProviderAPP.Models.Api;

public class SmsStatusResponse
{
    public bool Sucesso { get; set; }
    public bool PodeEnviar { get; set; }
    public int SegundosRestantes { get; set; }
    public string? Erro { get; set; }
}