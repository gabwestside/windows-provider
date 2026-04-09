using System.Collections.Generic;

namespace CredentialProviderAPP.Models.Api
{
    public class PasswordBlacklistResponse
    {
        public bool Sucesso { get; set; }
        public string? Erro { get; set; }
        public List<string> Palavras { get; set; } = new();
    }
}