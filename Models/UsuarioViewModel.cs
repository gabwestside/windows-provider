namespace CredentialProviderAPP.Models
{
    public class UsuarioViewModel
    {
        public string Tipo { get; set; }
        public string NomeCompleto { get; set; }
        public string Login { get; set; }
        public string DataCadastro { get; set; }

        public string MFAStatus { get; set; }  // ← ADICIONAR
    }
}