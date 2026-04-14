namespace CredentialProviderAPP.Models.Api;

public class TelefoneResponse
{
    public bool Sucesso { get; set; }
    public bool TemTelefone { get; set; }
    public string TelefoneMascarado { get; set; } = "";
    public string? Erro { get; set; }
}