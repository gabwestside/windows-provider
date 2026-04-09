namespace CredentialProviderAPP.Models
{
    public partial class UsuarioViewModel
    {
        public string Login { get; set; } = "";
        public string NomeCompleto { get; set; } = "";
        public string Tipo { get; set; } = "";
        public string DataCadastro { get; set; } = "";
        public string MFAStatus { get; set; } = "Não configurado";
        public string Email { get; set; } = "";
    }
}