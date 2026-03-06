using CredentialProviderAPP.Models;

namespace CredentialProviderAPP.Services
{
    public class UsuarioService
    {
        // Simulando um repositório de dados em memória
        private List<UsuarioViewModel> _usuarios;

        public UsuarioService()
        {
            _usuarios = new List<UsuarioViewModel>();
            InicializarDadosMock();
        }

        private void InicializarDadosMock()
        {
            _usuarios = new List<UsuarioViewModel>
            {
                new() {
                    DataCadastro = "12/12/2025 17:58",
                    Tipo = "Administrador",
                    NomeCompleto = "Diego Viana",
                    Login = "05037031330"
                },
                new() {
                    DataCadastro = "05/01/2026 14:09",
                    Tipo = "Usuário",
                    NomeCompleto = "38980665330 Alex teste",
                    Login = "38980665330"
                }
            };
        }

        public List<UsuarioViewModel> ObterTodosUsuarios()
        {
            return new List<UsuarioViewModel>(_usuarios);
        }

        public void AdicionarUsuario(UsuarioViewModel usuario)
        {
            _usuarios.Add(usuario);
        }

        public bool AtualizarUsuario(UsuarioViewModel usuarioAtualizado)
        {
            var usuario = _usuarios.FirstOrDefault(u => u.Login == usuarioAtualizado.Login);
            if (usuario != null)
            {
                usuario.NomeCompleto = usuarioAtualizado.NomeCompleto;
                usuario.Tipo = usuarioAtualizado.Tipo;
                usuario.DataCadastro = usuarioAtualizado.DataCadastro;
                return true;
            }
            return false;
        }

        public bool ExcluirUsuario(string login)
        {
            var usuario = _usuarios.FirstOrDefault(u => u.Login == login);
            if (usuario != null)
            {
                _usuarios.Remove(usuario);
                return true;
            }
            return false;
        }

        public UsuarioViewModel? ObterUsuarioPorLogin(string login)
        {
            return _usuarios.FirstOrDefault(u => u.Login == login);
        }

        public void ImportarUsuarios(List<UsuarioViewModel> novosUsuarios)
        {
            foreach (var usuario in novosUsuarios)
            {
                if (!_usuarios.Any(u => u.Login == usuario.Login))
                {
                    _usuarios.Add(usuario);
                }
            }
        }
    }
}